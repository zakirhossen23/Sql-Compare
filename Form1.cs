using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sql_Compare
{
    public partial class Form1 : Form
    {
        private CompareResult _lastResult;
        private string _localFilePath;
        private string _serverFilePath;
        private bool _isUpdatingCheck;
        private int _detailFontSize = 10;
        private RichTextBox _txtDetail;
        private Label _lblFontSize;

        // Color constants
        private static readonly Color ColorAdded = Color.FromArgb(221, 255, 221);
        private static readonly Color ColorRemoved = Color.FromArgb(255, 221, 221);
        private static readonly Color ColorModified = Color.FromArgb(255, 255, 221);

        public Form1()
        {
            InitializeComponent();
            SetupImageList();
            treeResults.CheckBoxes = true;
            treeResults.AfterCheck += TreeResults_AfterCheck;
            SetupDetailPanel();
        }

        private void SetupImageList()
        {
            // Create simple colored icons programmatically
            imageList.Images.Clear();
            
            // Index 0: Green circle (added)
            var bmpAdded = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmpAdded))
            {
                g.FillEllipse(Brushes.Green, 2, 2, 12, 12);
            }
            imageList.Images.Add(bmpAdded);
            
            // Index 1: Red circle (removed)
            var bmpRemoved = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmpRemoved))
            {
                g.FillEllipse(Brushes.Red, 2, 2, 12, 12);
            }
            imageList.Images.Add(bmpRemoved);
            
            // Index 2: Yellow circle (modified)
            var bmpModified = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmpModified))
            {
                g.FillEllipse(Brushes.Orange, 2, 2, 12, 12);
            }
            imageList.Images.Add(bmpModified);
            
            // Index 3: Blue circle (info)
            var bmpInfo = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmpInfo))
            {
                g.FillEllipse(Brushes.DodgerBlue, 2, 2, 12, 12);
            }
            imageList.Images.Add(bmpInfo);
            
            // Index 4: Gray circle (neutral)
            var bmpNeutral = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmpNeutral))
            {
                g.FillEllipse(Brushes.Gray, 2, 2, 12, 12);
            }
            imageList.Images.Add(bmpNeutral);

            treeResults.ImageList = imageList;
        }

        private void BtnBrowseLocal_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _localFilePath = openFileDialog.FileName;
                txtLocalFile.Text = Path.GetFileName(_localFilePath);
                txtLocalFile.Tag = _localFilePath;
                txtLocalFile.ForeColor = SystemColors.WindowText;
                UpdateStatus($"Local file selected: {_localFilePath}");
            }
        }

        private void BtnBrowseServer_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _serverFilePath = openFileDialog.FileName;
                txtServerFile.Text = Path.GetFileName(_serverFilePath);
                txtServerFile.Tag = _serverFilePath;
                txtServerFile.ForeColor = SystemColors.WindowText;
                UpdateStatus($"Server file selected: {_serverFilePath}");
            }
        }

        private void BtnCompare_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_localFilePath) || string.IsNullOrEmpty(_serverFilePath))
            {
                MessageBox.Show("Please select both Local and Server SQL files first.", "Files Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!File.Exists(_localFilePath) || !File.Exists(_serverFilePath))
            {
                MessageBox.Show("One or both selected files no longer exist. Please re-select.", "File Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                UpdateStatus("Parsing SQL files...");
                Application.DoEvents();

                var parser = new SqlParser();
                var localSchema = parser.ParseFile(_localFilePath);
                var serverSchema = parser.ParseFile(_serverFilePath);

                UpdateStatus($"Local: {localSchema.Tables.Count} tables, Server: {serverSchema.Tables.Count} tables. Comparing...");
                Application.DoEvents();

                var comparer = new SqlComparer();
                _lastResult = comparer.Compare(localSchema, serverSchema, _localFilePath, _serverFilePath);

                PopulateTreeView(_lastResult);
                btnExport.Enabled = _lastResult.Items.Count > 0;

                UpdateStatus($"Compare complete. Found {_lastResult.Items.Count} difference(s).");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during comparison:\n\n{ex.Message}", "Compare Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Error during comparison.");
            }
        }

        private void PopulateTreeView(CompareResult result)
        {
            treeResults.Nodes.Clear();
            listDetails.Items.Clear();

            // Store all items for export filtering
            treeResults.Tag = result;

            var groups = result.Items
                .GroupBy(i => i.Type)
                .OrderBy(g => GetGroupSortOrder(g.Key))
                .ToList();

            foreach (var group in groups)
            {
                var typeName = GetGroupDisplayName(group.Key);
                var count = group.Count();
                var groupNode = new TreeNode($"{typeName} ({count})");
                groupNode.Tag = "group";

                // Set group node color
                SetNodeColorByType(groupNode, group.Key);

                foreach (var item in group.OrderBy(i => i.TableName).ThenBy(i => i.ObjectName))
                {
                    var node = new TreeNode($"[{item.TableName}] {item.Description}");
                    node.Tag = item;
                    node.Checked = true; // All checked by default
                    SetNodeColorByType(node, item.Type);
                    groupNode.Nodes.Add(node);
                }

                treeResults.Nodes.Add(groupNode);
                // Default group checked if it has items
                groupNode.Checked = count > 0;
            }

            if (result.Items.Count == 0)
            {
                treeResults.Nodes.Add(new TreeNode("No differences found - schemas are identical.") { Tag = "none" });
            }

            treeResults.ExpandAll();
        }

        private void TreeResults_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingCheck) return;

            try
            {
                _isUpdatingCheck = true;

                if (e.Node.Tag is string s && s == "group")
                {
                    // Group node checked/unchecked -> sync all children
                    foreach (TreeNode child in e.Node.Nodes)
                    {
                        child.Checked = e.Node.Checked;
                    }
                }
                else if (e.Node.Tag is DiffItem)
                {
                    // Child node changed -> update parent group state
                    if (e.Node.Parent != null)
                    {
                        int checkedCount = 0;
                        foreach (TreeNode child in e.Node.Parent.Nodes)
                        {
                            if (child.Checked) checkedCount++;
                        }
                        e.Node.Parent.Checked = checkedCount > 0;
                    }
                }

                UpdateExportButtonState();
            }
            finally
            {
                _isUpdatingCheck = false;
            }
        }

        private void UpdateExportButtonState()
        {
            if (_lastResult == null) { btnExport.Enabled = false; return; }

            int checkedCount = 0;
            foreach (TreeNode groupNode in treeResults.Nodes)
            {
                foreach (TreeNode child in groupNode.Nodes)
                {
                    if (child.Checked) checkedCount++;
                }
            }
            btnExport.Enabled = checkedCount > 0;
        }

        private void SetNodeColorByType(TreeNode node, DiffType type)
        {
            switch (type)
            {
                case DiffType.TableAdded:
                case DiffType.ColumnAdded:
                case DiffType.PrimaryKeyAdded:
                case DiffType.ForeignKeyAdded:
                case DiffType.IndexAdded:
                case DiffType.UniqueConstraintAdded:
                case DiffType.CheckConstraintAdded:
                case DiffType.RowAdded:
                    node.ImageIndex = 0;
                    node.SelectedImageIndex = 0;
                    break;
                case DiffType.TableRemoved:
                case DiffType.ColumnRemoved:
                case DiffType.PrimaryKeyRemoved:
                case DiffType.ForeignKeyRemoved:
                case DiffType.IndexRemoved:
                case DiffType.UniqueConstraintRemoved:
                case DiffType.CheckConstraintRemoved:
                case DiffType.RowRemoved:
                    node.ImageIndex = 1;
                    node.SelectedImageIndex = 1;
                    break;
                case DiffType.ColumnModified:
                case DiffType.PrimaryKeyModified:
                case DiffType.IndexModified:
                case DiffType.RowModified:
                    node.ImageIndex = 2;
                    node.SelectedImageIndex = 2;
                    break;
                default:
                    node.ImageIndex = 4;
                    node.SelectedImageIndex = 4;
                    break;
            }
        }

        private int GetGroupSortOrder(DiffType type)
        {
            switch (type)
            {
                case DiffType.TableAdded: return 0;
                case DiffType.TableRemoved: return 1;
                case DiffType.ColumnAdded: return 2;
                case DiffType.ColumnRemoved: return 3;
                case DiffType.ColumnModified: return 4;
                case DiffType.PrimaryKeyAdded: return 5;
                case DiffType.PrimaryKeyRemoved: return 6;
                case DiffType.PrimaryKeyModified: return 7;
                case DiffType.ForeignKeyAdded: return 8;
                case DiffType.ForeignKeyRemoved: return 9;
                case DiffType.IndexAdded: return 10;
                case DiffType.IndexRemoved: return 11;
                case DiffType.IndexModified: return 12;
                case DiffType.UniqueConstraintAdded: return 13;
                case DiffType.UniqueConstraintRemoved: return 14;
                case DiffType.CheckConstraintAdded: return 15;
                case DiffType.CheckConstraintRemoved: return 16;
                case DiffType.RowAdded: return 17;
                case DiffType.RowRemoved: return 18;
                case DiffType.RowModified: return 19;
                default: return 99;
            }
        }

        private string GetGroupDisplayName(DiffType type)
        {
            switch (type)
            {
                case DiffType.TableAdded: return "Tables Only in Local";
                case DiffType.TableRemoved: return "Tables Only in Server";
                case DiffType.ColumnAdded: return "Columns Only in Local";
                case DiffType.ColumnRemoved: return "Columns Only in Server";
                case DiffType.ColumnModified: return "Columns Modified";
                case DiffType.PrimaryKeyAdded: return "Primary Keys Only in Local";
                case DiffType.PrimaryKeyRemoved: return "Primary Keys Only in Server";
                case DiffType.PrimaryKeyModified: return "Primary Keys Modified";
                case DiffType.ForeignKeyAdded: return "Foreign Keys Only in Local";
                case DiffType.ForeignKeyRemoved: return "Foreign Keys Only in Server";
                case DiffType.IndexAdded: return "Indexes Only in Local";
                case DiffType.IndexRemoved: return "Indexes Only in Server";
                case DiffType.IndexModified: return "Indexes Modified";
                case DiffType.UniqueConstraintAdded: return "Unique Constraints Only in Local";
                case DiffType.UniqueConstraintRemoved: return "Unique Constraints Only in Server";
                case DiffType.CheckConstraintAdded: return "Check Constraints Only in Local";
                case DiffType.CheckConstraintRemoved: return "Check Constraints Only in Server";
                case DiffType.RowAdded: return "Rows Only in Local";
                case DiffType.RowRemoved: return "Rows Only in Server";
                case DiffType.RowModified: return "Rows Modified";
                default: return type.ToString();
            }
        }

        private void TreeResults_AfterSelect(object sender, TreeViewEventArgs e)
        {
            listDetails.Items.Clear();

            if (e.Node?.Tag is DiffItem item)
            {
                ShowDiffDetails(item);
            }
            else if (e.Node?.Tag is string s && s == "group")
            {
                // Show summary for the group
                var summaryItem = new ListViewItem("Group Summary");
                summaryItem.SubItems.Add(e.Node.Text);
                summaryItem.SubItems.Add("");
                listDetails.Items.Add(summaryItem);
            }
        }

        private void ShowDiffDetails(DiffItem item)
        {
            listDetails.Items.Clear();

            AddDetail("Type", GetGroupDisplayName(item.Type), "");
            AddDetail("Table", "", item.TableName);
            AddDetail("Object", "", item.ObjectName);
            AddDetail("Description", "", item.Description);

            // Always show Server (top) and Local (bottom)
            AddDetail("Server",
                string.IsNullOrEmpty(item.DetailOld) ? "(not present)" : item.DetailOld,
                "");
            AddDetail("Local",
                "",
                string.IsNullOrEmpty(item.DetailNew) ? "(not present)" : item.DetailNew);

            if (!string.IsNullOrEmpty(item.SqlScript))
            {
                AddDetail("Generated SQL", "", item.SqlScript);
            }

            // Show full details in the scrollable text box
            ShowDetailInTextBox(item);
        }

        private void ShowDetailInTextBox(DiffItem item)
        {
            _txtDetail.Clear();

            var isAdded = item.Type == DiffType.TableAdded || item.Type == DiffType.ColumnAdded
                       || item.Type == DiffType.IndexAdded || item.Type == DiffType.ForeignKeyAdded
                       || item.Type == DiffType.PrimaryKeyAdded || item.Type == DiffType.UniqueConstraintAdded
                       || item.Type == DiffType.CheckConstraintAdded || item.Type == DiffType.RowAdded;
            var isRemoved = item.Type == DiffType.TableRemoved || item.Type == DiffType.ColumnRemoved
                         || item.Type == DiffType.IndexRemoved || item.Type == DiffType.ForeignKeyRemoved
                         || item.Type == DiffType.PrimaryKeyRemoved || item.Type == DiffType.UniqueConstraintRemoved
                         || item.Type == DiffType.CheckConstraintRemoved || item.Type == DiffType.RowRemoved;
            var isModified = item.Type == DiffType.ColumnModified || item.Type == DiffType.IndexModified
                          || item.Type == DiffType.PrimaryKeyModified || item.Type == DiffType.RowModified;

            // Info header
            AppendColoredLine($"Type: {GetGroupDisplayName(item.Type)}", Color.Black, Color.White);
            AppendColoredLine($"Table: {item.TableName}", Color.Black, Color.White);
            AppendColoredLine($"Object: {item.ObjectName}", Color.Black, Color.White);
            AppendColoredLine($"Description: {item.Description}", Color.Black, Color.White);
            AppendLine("");

            if (isAdded)
            {
                AppendColoredLine("+++ Added +++", Color.DarkGreen, Color.FromArgb(220, 255, 220));
                if (!string.IsNullOrEmpty(item.DetailNew))
                    AppendColoredText(item.DetailNew, Color.DarkGreen, Color.FromArgb(220, 255, 220));
                else if (!string.IsNullOrEmpty(item.SqlScript))
                    AppendColoredText(item.SqlScript, Color.DarkGreen, Color.FromArgb(220, 255, 220));
            }
            else if (isRemoved)
            {
                AppendColoredLine("--- Removed ---", Color.DarkRed, Color.FromArgb(255, 220, 220));
                if (!string.IsNullOrEmpty(item.DetailOld))
                    AppendColoredText(item.DetailOld, Color.DarkRed, Color.FromArgb(255, 220, 220));
                else if (!string.IsNullOrEmpty(item.SqlScript))
                    AppendColoredText(item.SqlScript, Color.DarkRed, Color.FromArgb(255, 220, 220));
            }
            else if (isModified)
            {
                ShowLineDiff(item.DetailOld, item.DetailNew, "Server (Old)", "Local (New)");
                if (!string.IsNullOrEmpty(item.SqlScript))
                {
                    AppendLine("=== Migration SQL ===");
                    AppendColoredText(item.SqlScript, Color.DarkBlue, Color.FromArgb(230, 240, 255));
                }
            }
            else
            {
                ShowLineDiff(item.DetailOld, item.DetailNew, "Server (Old)", "Local (New)");
                if (!string.IsNullOrEmpty(item.SqlScript))
                {
                    AppendLine("=== Migration SQL ===");
                    AppendColoredText(item.SqlScript, Color.DarkBlue, SystemColors.Window);
                }
            }

            _txtDetail.SelectionStart = 0;
            _txtDetail.SelectionLength = 0;
            _txtDetail.ScrollToCaret();
        }

        private enum DiffLineKind { Unchanged, Added, Removed }

        private struct DiffLine
        {
            public DiffLineKind Kind;
            public string Text;
        }

        /// <summary>
        /// Compute a simple LCS-based line diff between two texts.
        /// </summary>
        private List<DiffLine> ComputeLineDiff(string oldText, string newText)
        {
            var oldLines = oldText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var newLines = newText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Build LCS table
            int m = oldLines.Length, n = newLines.Length;
            var dp = new int[m + 1, n + 1];
            for (int i = 1; i <= m; i++)
                for (int j = 1; j <= n; j++)
                    if (string.Equals(oldLines[i - 1].Trim(), newLines[j - 1].Trim(), StringComparison.Ordinal))
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    else
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);

            // Backtrack to find matching pairs
            var matches = new List<(int oldIdx, int newIdx)>();
            int x = m, y = n;
            while (x > 0 && y > 0)
            {
                if (string.Equals(oldLines[x - 1].Trim(), newLines[y - 1].Trim(), StringComparison.Ordinal))
                {
                    matches.Add((x - 1, y - 1));
                    x--; y--;
                }
                else if (dp[x - 1, y] >= dp[x, y - 1])
                    x--;
                else
                    y--;
            }
            matches.Reverse();

            // Walk through producing diff lines
            var result = new List<DiffLine>();
            int oi = 0, ni = 0, mi = 0;
            while (oi < m || ni < n)
            {
                if (mi < matches.Count)
                {
                    var (matchOld, matchNew) = matches[mi];
                    // Removed lines from old before the match
                    while (oi < matchOld)
                    {
                        result.Add(new DiffLine { Kind = DiffLineKind.Removed, Text = oldLines[oi] });
                        oi++;
                    }
                    // Added lines from new before the match
                    while (ni < matchNew)
                    {
                        result.Add(new DiffLine { Kind = DiffLineKind.Added, Text = newLines[ni] });
                        ni++;
                    }
                    // Matching line (unchanged)
                    result.Add(new DiffLine { Kind = DiffLineKind.Unchanged, Text = oldLines[oi] });
                    oi++; ni++; mi++;
                }
                else
                {
                    while (oi < m)
                    {
                        result.Add(new DiffLine { Kind = DiffLineKind.Removed, Text = oldLines[oi] });
                        oi++;
                    }
                    while (ni < n)
                    {
                        result.Add(new DiffLine { Kind = DiffLineKind.Added, Text = newLines[ni] });
                        ni++;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Render a line-by-line diff into the RichTextBox with git-style coloring.
        /// </summary>
        private void ShowLineDiff(string oldText, string newText, string oldLabel, string newLabel)
        {
            bool hasOld = !string.IsNullOrEmpty(oldText);
            bool hasNew = !string.IsNullOrEmpty(newText);

            if (hasOld && hasNew)
            {
                var diffLines = ComputeLineDiff(oldText, newText);

                // Check if there are any differences at all
                bool hasChanges = diffLines.Any(l => l.Kind != DiffLineKind.Unchanged);

                if (hasChanges)
                {
                    foreach (var line in diffLines)
                    {
                        switch (line.Kind)
                        {
                            case DiffLineKind.Unchanged:
                                AppendLine(line.Text);
                                break;
                            case DiffLineKind.Removed:
                                AppendColoredLine(line.Text, Color.DarkRed, Color.FromArgb(255, 220, 220));
                                break;
                            case DiffLineKind.Added:
                                AppendColoredLine(line.Text, Color.DarkGreen, Color.FromArgb(220, 255, 220));
                                break;
                        }
                    }
                }
                else
                {
                    // No actual line differences — show header blocks as before
                    if (hasOld)
                    {
                        AppendColoredLine($"--- {oldLabel}", Color.DarkRed, Color.FromArgb(255, 220, 220));
                        AppendColoredText(oldText, Color.DarkRed, Color.FromArgb(255, 220, 220));
                        AppendLine("");
                    }
                    if (hasNew)
                    {
                        AppendColoredLine($"+++ {newLabel}", Color.DarkGreen, Color.FromArgb(220, 255, 220));
                        AppendColoredText(newText, Color.DarkGreen, Color.FromArgb(220, 255, 220));
                        AppendLine("");
                    }
                }
            }
            else if (hasOld)
            {
                AppendColoredLine($"--- {oldLabel}", Color.DarkRed, Color.FromArgb(255, 220, 220));
                AppendColoredText(oldText, Color.DarkRed, Color.FromArgb(255, 220, 220));
                AppendLine("");
            }
            else if (hasNew)
            {
                AppendColoredLine($"+++ {newLabel}", Color.DarkGreen, Color.FromArgb(220, 255, 220));
                AppendColoredText(newText, Color.DarkGreen, Color.FromArgb(220, 255, 220));
                AppendLine("");
            }
        }

        private void AppendColoredLine(string text, Color foreColor, Color backColor)
        {
            AppendColoredText(text + Environment.NewLine, foreColor, backColor);
        }

        private void AppendColoredText(string text, Color foreColor, Color backColor)
        {
            _txtDetail.SelectionStart = _txtDetail.TextLength;
            _txtDetail.SelectionLength = 0;
            _txtDetail.SelectionColor = foreColor;
            _txtDetail.SelectionBackColor = backColor;
            _txtDetail.SelectedText = text;
        }

        private void AppendLine(string text)
        {
            AppendColoredText(text + Environment.NewLine, Color.Black, SystemColors.Window);
        }

        private void AddDetail(string property, string oldValue, string newValue)
        {
            var item = new ListViewItem(property);
            item.SubItems.Add(oldValue);
            item.SubItems.Add(newValue);
            listDetails.Items.Add(item);
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_lastResult == null || _lastResult.Items.Count == 0)
            {
                MessageBox.Show("No differences to export. Run a comparison first.", "Nothing to Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Collect checked items
            var checkedItems = new List<DiffItem>();
            foreach (TreeNode groupNode in treeResults.Nodes)
            {
                foreach (TreeNode child in groupNode.Nodes)
                {
                    if (child.Checked && child.Tag is DiffItem item)
                    {
                        checkedItems.Add(item);
                    }
                }
            }

            if (checkedItems.Count == 0)
            {
                MessageBox.Show("No changes selected. Check the items you want to include in the export.",
                    "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            saveFileDialog.FileName = $"SchemaChanges_{DateTime.Now:yyyyMMdd_HHmmss}.sql";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Create a filtered result for export
                    var filteredResult = new CompareResult
                    {
                        LocalFileName = _lastResult.LocalFileName,
                        ServerFileName = _lastResult.ServerFileName
                    };
                    filteredResult.Items.AddRange(checkedItems);

                    var script = filteredResult.GenerateExportScript();
                    File.WriteAllText(saveFileDialog.FileName, script);
                    UpdateStatus($"Export saved to: {saveFileDialog.FileName} ({checkedItems.Count} changes)");

                    var result = MessageBox.Show(
                        $"Changes exported successfully to:\n{saveFileDialog.FileName}\n\nDo you want to open the file?",
                        "Export Complete",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving export:\n\n{ex.Message}", "Export Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateStatus(string message)
        {
            lblStatus.Text = message;
            Application.DoEvents();
        }

        #region Detail panel: scrollable text area with word wrap

        private void SetupDetailPanel()
        {
            // Create a RichTextBox for colored diff display
            _txtDetail = new RichTextBox
            {
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", _detailFontSize),
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            // Toolbar with font size +/- buttons
            var btnDec = new Button { Text = "−", Width = 22, Height = 22, Margin = new Padding(1, 2, 1, 0) };
            var btnInc = new Button { Text = "+", Width = 22, Height = 22, Margin = new Padding(1, 2, 1, 0) };
            _lblFontSize = new Label
            {
                Text = $"Font: {_detailFontSize}",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 4, 4, 0),
                Font = new Font("Segoe UI", 8F)
            };

            btnDec.Click += (s, e) => { _detailFontSize = Math.Max(8, _detailFontSize - 1); UpdateDetailFont(); };
            btnInc.Click += (s, e) => { _detailFontSize = Math.Min(24, _detailFontSize + 1); UpdateDetailFont(); };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 26,
                FlowDirection = FlowDirection.RightToLeft,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(0, 1, 4, 0)
            };

            toolbar.Controls.Add(btnInc);
            toolbar.Controls.Add(btnDec);
            toolbar.Controls.Add(_lblFontSize);

            // Split the right panel: ListView on top, TextBox on bottom with a splitter
            var splitInner = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 4,
                SplitterDistance = 220
            };

            // Top: toolbar + listDetails
            var topPanel = new Panel { Dock = DockStyle.Fill };
            topPanel.Controls.Add(listDetails);
            topPanel.Controls.Add(toolbar);
            listDetails.Dock = DockStyle.Fill;

            splitInner.Panel1.Controls.Add(topPanel);
            splitInner.Panel2.Controls.Add(_txtDetail);

            // Replace the right panel contents
            splitContainer.Panel2.Controls.Clear();
            splitContainer.Panel2.Controls.Add(splitInner);
        }

        private void UpdateDetailFont()
        {
            _txtDetail.Font = new Font("Consolas", _detailFontSize);
            _lblFontSize.Text = $"Font: {_detailFontSize}";
        }

        #endregion
    }
}
