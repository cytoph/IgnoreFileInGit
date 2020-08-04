using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace IgnoreFilesInGit
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class IgnoreFileInGit
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("4a50411c-863d-4fe4-b426-eeb86308b762");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="IgnoreFileInGit"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private IgnoreFileInGit(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += BeforeQueryStatus;
            commandService.AddCommand(menuItem);

            menuItem.Enabled = true;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static IgnoreFileInGit Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in IgnoreFileInGit's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new IgnoreFileInGit(package, commandService);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(Package.GetGlobalService(typeof(EnvDTE.DTE)) is EnvDTE.DTE dte))
                return;

            string solutionPath = Path.GetDirectoryName(dte.Solution.FileName);
            List<string> ignoredFiles = GetIgnoredFiles(solutionPath);

            var filePath = GetSelectedFilePath(dte);

            var command = sender as OleMenuCommand;

            if (command == null)
                return;

            command.Checked = ignoredFiles.Contains(filePath);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(Package.GetGlobalService(typeof(EnvDTE.DTE)) is EnvDTE.DTE dte))
                return;

            string solutionPath = Path.GetDirectoryName(dte.Solution.FileName);
            List<string> ignoredFiles = GetIgnoredFiles(solutionPath);

            string filePath = GetSelectedFilePath(dte);
            bool isIgnored = ignoredFiles.Contains(filePath);

            var errors = SetIgnoreStatus(filePath, !isIgnored);

            if (errors?.Count > 0)
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    string.Join(Environment.NewLine, errors),
                    "Error on set files ignore state",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        #region Helpers

        private List<string> GetIgnoredFiles(string solutionPath)
        {
            System.Collections.ObjectModel.Collection<PSObject> results;

            using (var powerShell = PowerShell.Create())
            {
                powerShell.AddScript($"cd \"{ solutionPath }\"");
                powerShell.AddScript("git ls-files -v .");
                results = powerShell.Invoke();
            }

            return results.Select(r => (string)r.BaseObject)
                .Where(s => s.StartsWith("S "))
                .Select(s => Path.Combine(solutionPath, s.Substring(2).Replace('/', '\\')))
                .ToList();
        }

        private List<string> SetIgnoreStatus(string filePath, bool setIgnored)
        {
            string solutionPath = Path.GetDirectoryName(filePath);
            string relativePath = Path.GetFileName(filePath);

            string script = "git update-index --" + (setIgnored ? "" : "no-") + "skip-worktree \"" + relativePath + "\"";

            PSDataCollection<ErrorRecord> errors;

            using (var powerShell = PowerShell.Create())
            {
                powerShell.AddScript($"cd \"{ solutionPath }\"")
                    .AddScript(script)
                    .Invoke();

                errors = powerShell.Streams.Error;
            }

            if (errors?.Count > 0)
                return errors.Select(er => er.ToString()).ToList();

            return null;
        }

        private string GetSelectedFilePath(EnvDTE.DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE.SelectedItems selectedItems = dte.SelectedItems;

            if (!(selectedItems?.Count > 0))
                return null;

            var item = selectedItems.Item(1);

            if (item.Project != null)
                return item.Project.FileName;
            else if (item.ProjectItem != null)
            {
                if (item.ProjectItem.Properties != null)
                    return (string)item.ProjectItem.Properties.Item("FullPath").Value;
                else
                    return item.ProjectItem.FileNames[1];
            }
            else // is Solution
                return dte.Solution.FileName;
        }

        #endregion // Helpers
    }
}
