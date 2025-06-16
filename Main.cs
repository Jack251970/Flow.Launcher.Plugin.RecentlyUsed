using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.RecentlyUsed.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.RecentlyUsed
{
    public class Main : IPlugin
    {
        private PluginInitContext context;
        private string recentFolder;

        public void Init(PluginInitContext context)
        {
            this.context = context;
            recentFolder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
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

                // �˻��� ���͸� (�⺻�� ��ũ ���ϸ� ���)
                if (!string.IsNullOrEmpty(searchTerm) &&
                    !fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    continue;

                string targetPath = ShellLinkHelper.ResolveShortcut(lnkPath);

                // Ÿ���� ���� ��� fallback
                if (string.IsNullOrEmpty(targetPath))
                    targetPath = fileName;

                string title = Path.GetFileName(targetPath);
                string subtitle = Path.GetDirectoryName(targetPath);
                string icoPath = lnkPath;

                // �������� ������ (Ư�� OneNote ��)
                if (fileName.Contains("--"))
                {
                    var protocol = fileName.Split("--")[0].Replace('-', ':');
                    var protocolIcon = ProtocolIconHelper.GetProtocolIconPath(protocol);
                    if (!string.IsNullOrEmpty(protocolIcon))
                        icoPath = protocolIcon;
                }

                results.Add(new Result
                {
                    Title = title,
                    SubTitle = subtitle,
                    IcoPath = icoPath,
                    Action = c =>
                    {
                        try
                        {
                            // ���� �ڵ� rewrite ����
                            if (Directory.Exists(targetPath) && title.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                            {
                                context.API.ChangeQuery(targetPath + "\\", true);
                                return false;
                            }

                            context.API.ShellRun(lnkPath);
                        }
                        catch { }
                        return true;
                    }
                });
            }

            return results;
        }
    }
}
