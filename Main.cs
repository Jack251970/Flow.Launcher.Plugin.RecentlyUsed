using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.RecentlyUsed;
using Flow.Launcher.Plugin.RecentlyUsed.Helper;
using Flow.Launcher.Plugin.RecentlyUsed.Views;
using System.Windows.Controls;
using System.IO;
using System.Runtime.InteropServices;

public class Main : IPlugin, ISettingProvider
{
    private PluginInitContext context;
    private string recentFolder;
    private Settings settings;

    public void Init(PluginInitContext context)
    {
        this.context = context;
        recentFolder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        settings = context.API.LoadSettingJsonStorage<Settings>();
    }

    public Control CreateSettingPanel()
    {
        return new SettingsUserControl(settings);
    }

    public List<Result> Query(Query query)
    {
        var results = new List<Result>();

        if (!Directory.Exists(recentFolder))
            return results;

        var searchTerm = query.Search?.Trim() ?? "";

        var files = Directory.GetFiles(recentFolder, "*.lnk")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        foreach (var fileInfo in files)
        {
            var lnkPath = fileInfo.FullName;
            var fileName = Path.GetFileNameWithoutExtension(lnkPath);

            string targetPath = ShellLinkHelper.ResolveShortcut(lnkPath);

            if (string.IsNullOrEmpty(targetPath))
                continue;

            // �α�(������, ���߿� ���� ����)
            // context.API.LogInfo($"File: {fileName}, Target: {targetPath}");

            bool isFolder = Directory.Exists(targetPath);
            bool isFile = File.Exists(targetPath);
            bool isUrl = Uri.IsWellFormedUriString(targetPath, UriKind.Absolute) ||
             targetPath.StartsWith("onenote:", StringComparison.OrdinalIgnoreCase) ||
             targetPath.StartsWith("onenotehttps:", StringComparison.OrdinalIgnoreCase);

            // URL�� �׻� ǥ�õǵ��� ���� ����
            if (!settings.ShowFolders && isFolder)
                continue;

            // ���ϵ� �ƴϰ� ������ �ƴϰ� URL�� �ƴϸ� �ǳʶٱ�
            if (!isFile && !isFolder && !isUrl)
                continue;

            // ��� ������ ��ü �̸� (Ȯ���� ����)
            string targetFileName = Path.GetFileName(targetPath);
            
            // ����̺� ��Ʈ ó��
            string title = Path.GetFileName(targetPath);
            string subTitle = Path.GetDirectoryName(targetPath);
            bool isDriveRoot = false;

            if (string.IsNullOrEmpty(title) && Directory.Exists(targetPath))
            {
                try
                {
                    // ����̺� ��Ʈ Ȯ��
                    isDriveRoot = Path.GetPathRoot(targetPath) == targetPath;

                    if (isDriveRoot)
                    {
                        // ����̺� ��Ʈ�� lnk ���ϸ�(Ȯ���� ����)�� ���
                        title = fileName;  // fileName�� �̹� ��ܿ��� Path.GetFileNameWithoutExtension(lnkPath)�� Ȯ���� ���ܵ�
                        
                        // ���ϸ� ���� ����̺� ���ڸ� �ִ� ���(��: "���� ��ũ (C)")��� �ݷ�(:) �߰�
                        if (title.EndsWith(")"))
                        {
                            int openParenIndex = title.LastIndexOf('(');
                            if (openParenIndex > 0 && openParenIndex + 2 < title.Length)
                            {
                                char driveLetter = title[openParenIndex + 1];
                                if (char.IsLetter(driveLetter) && title[openParenIndex + 2] == ')')
                                {
                                    // "���� ��ũ (C)" -> "���� ��ũ (C:)"�� ��ȯ
                                    title = title.Insert(openParenIndex + 2, ":");
                                }
                            }
                        }
                        
                        subTitle = targetPath;  // ��� ������ subTitle�� ǥ��
                    }
                }
                catch
                {
                    title = targetPath;
                    subTitle = targetPath;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(title))
                    title = targetPath;
                if (string.IsNullOrEmpty(subTitle))
                    subTitle = targetPath;
            }

            // �˻�� ���� ��� ���ϸ�� Ȯ���ڸ� ��� �˻� (title�� subtitle�� �˻� ��� ����)
            if (!string.IsNullOrEmpty(searchTerm) &&
                !fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !targetFileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !subTitle.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !targetPath.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(new Result
            {
                Title = title,
                SubTitle = subTitle,
                IcoPath = lnkPath,
                Action = _ =>
                {
                    // ����̺� ��Ʈ �Ǵ� ������ ���
                    if (isDriveRoot || isFolder)
                    {
                        context.API.OpenDirectory(targetPath);
                        return true;
                    }
                    else
                    {
                        // lnk�� ����Ű�� ����� �̹� ����ǥ�� ������ �ִٸ� ����ǥ ����
                        string normalizedTarget = targetPath;
                        if (normalizedTarget.StartsWith("\"") && normalizedTarget.EndsWith("\""))
                            normalizedTarget = normalizedTarget.Substring(1, normalizedTarget.Length - 2);

                        // explorer.exe�� �׻� ���� (����/�ѱ�/����ǥ ��� ����)
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{normalizedTarget}\"",
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                        return true;
                    }
                },
                AddSelectedCount = false
            });
        }

        return results;
    }

    private static string GetVolumeLabel(string driveRoot)
    {
        var label = new System.Text.StringBuilder(261);
        var fs = new System.Text.StringBuilder(261);
        uint serial = 0, maxLen = 0, flags = 0;
        bool ok = GetVolumeInformation(driveRoot, label, (uint)label.Capacity, out serial, out maxLen, out flags, fs, (uint)fs.Capacity);
        return ok ? label.ToString() : string.Empty;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        System.Text.StringBuilder volumeNameBuffer,
        uint volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        System.Text.StringBuilder fileSystemNameBuffer,
        uint nFileSystemNameSize);
}

// ĳ�� �׸��� ���� Ŭ����
[Serializable]
public class RecentItem
{
    public string LnkPath { get; set; }
    public string FileName { get; set; }
    public string TargetPath { get; set; }
    public string TargetFileName { get; set; }
    public string Title { get; set; }
    public string SubTitle { get; set; }
    public bool IsFolder { get; set; }
    public bool IsDriveRoot { get; set; }
}
