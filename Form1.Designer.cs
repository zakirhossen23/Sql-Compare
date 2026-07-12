namespace Sql_Compare
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TableLayoutPanel mainLayout;
        private System.Windows.Forms.Panel localPanel;
        private System.Windows.Forms.Panel serverPanel;
        private System.Windows.Forms.Label lblLocal;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox txtLocalFile;
        private System.Windows.Forms.TextBox txtServerFile;
        private System.Windows.Forms.Button btnBrowseLocal;
        private System.Windows.Forms.Button btnBrowseServer;
        private System.Windows.Forms.Button btnCompare;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.TreeView treeResults;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ListView listDetails;
        private System.Windows.Forms.ColumnHeader colProperty;
        private System.Windows.Forms.ColumnHeader colOldValue;
        private System.Windows.Forms.ColumnHeader colNewValue;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("Ready to compare");
            this.mainLayout = new System.Windows.Forms.TableLayoutPanel();
            this.localPanel = new System.Windows.Forms.Panel();
            this.lblLocal = new System.Windows.Forms.Label();
            this.txtLocalFile = new System.Windows.Forms.TextBox();
            this.btnBrowseLocal = new System.Windows.Forms.Button();
            this.serverPanel = new System.Windows.Forms.Panel();
            this.lblServer = new System.Windows.Forms.Label();
            this.txtServerFile = new System.Windows.Forms.TextBox();
            this.btnBrowseServer = new System.Windows.Forms.Button();
            this.btnCompare = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.treeResults = new System.Windows.Forms.TreeView();
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.listDetails = new System.Windows.Forms.ListView();
            this.colProperty = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colOldValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colNewValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.mainLayout.SuspendLayout();
            this.localPanel.SuspendLayout();
            this.serverPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainLayout
            // 
            this.mainLayout.ColumnCount = 6;
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.mainLayout.Controls.Add(this.localPanel, 0, 0);
            this.mainLayout.Controls.Add(this.serverPanel, 3, 0);
            this.mainLayout.Controls.Add(this.btnCompare, 2, 1);
            this.mainLayout.Controls.Add(this.btnExport, 5, 1);
            this.mainLayout.Controls.Add(this.splitContainer, 0, 2);
            this.mainLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayout.Location = new System.Drawing.Point(0, 0);
            this.mainLayout.Name = "mainLayout";
            this.mainLayout.Padding = new System.Windows.Forms.Padding(10);
            this.mainLayout.RowCount = 3;
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayout.Size = new System.Drawing.Size(1200, 650);
            this.mainLayout.TabIndex = 0;
            // 
            // localPanel
            // 
            this.mainLayout.SetColumnSpan(this.localPanel, 2);
            this.localPanel.Controls.Add(this.lblLocal);
            this.localPanel.Controls.Add(this.txtLocalFile);
            this.localPanel.Controls.Add(this.btnBrowseLocal);
            this.localPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.localPanel.Location = new System.Drawing.Point(13, 13);
            this.localPanel.Name = "localPanel";
            this.localPanel.Size = new System.Drawing.Size(493, 29);
            this.localPanel.TabIndex = 0;
            // 
            // lblLocal
            // 
            this.lblLocal.AutoSize = true;
            this.lblLocal.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLocal.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(212)))));
            this.lblLocal.Location = new System.Drawing.Point(3, 7);
            this.lblLocal.Name = "lblLocal";
            this.lblLocal.Size = new System.Drawing.Size(49, 17);
            this.lblLocal.TabIndex = 0;
            this.lblLocal.Text = "Local:";
            // 
            // txtLocalFile
            // 
            this.txtLocalFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLocalFile.Location = new System.Drawing.Point(58, 5);
            this.txtLocalFile.Name = "txtLocalFile";
            this.txtLocalFile.ReadOnly = true;
            this.txtLocalFile.Size = new System.Drawing.Size(339, 22);
            this.txtLocalFile.TabIndex = 1;
            this.txtLocalFile.Text = "Click Browse to select Local SQL file...";
            // 
            // btnBrowseLocal
            // 
            this.btnBrowseLocal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseLocal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(212)))));
            this.btnBrowseLocal.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBrowseLocal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnBrowseLocal.ForeColor = System.Drawing.Color.White;
            this.btnBrowseLocal.Location = new System.Drawing.Point(403, 3);
            this.btnBrowseLocal.Name = "btnBrowseLocal";
            this.btnBrowseLocal.Size = new System.Drawing.Size(87, 26);
            this.btnBrowseLocal.TabIndex = 2;
            this.btnBrowseLocal.Text = "Browse...";
            this.btnBrowseLocal.UseVisualStyleBackColor = false;
            this.btnBrowseLocal.Click += new System.EventHandler(this.BtnBrowseLocal_Click);
            // 
            // serverPanel
            // 
            this.mainLayout.SetColumnSpan(this.serverPanel, 2);
            this.serverPanel.Controls.Add(this.lblServer);
            this.serverPanel.Controls.Add(this.txtServerFile);
            this.serverPanel.Controls.Add(this.btnBrowseServer);
            this.serverPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.serverPanel.Location = new System.Drawing.Point(686, 13);
            this.serverPanel.Name = "serverPanel";
            this.serverPanel.Size = new System.Drawing.Size(494, 29);
            this.serverPanel.TabIndex = 1;
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblServer.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(204)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.lblServer.Location = new System.Drawing.Point(3, 7);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(49, 17);
            this.lblServer.TabIndex = 0;
            this.lblServer.Text = "Server:";
            // 
            // txtServerFile
            // 
            this.txtServerFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtServerFile.Location = new System.Drawing.Point(58, 5);
            this.txtServerFile.Name = "txtServerFile";
            this.txtServerFile.ReadOnly = true;
            this.txtServerFile.Size = new System.Drawing.Size(340, 22);
            this.txtServerFile.TabIndex = 1;
            this.txtServerFile.Text = "Click Browse to select Server SQL file...";
            // 
            // btnBrowseServer
            // 
            this.btnBrowseServer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseServer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(204)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.btnBrowseServer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBrowseServer.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnBrowseServer.ForeColor = System.Drawing.Color.White;
            this.btnBrowseServer.Location = new System.Drawing.Point(404, 3);
            this.btnBrowseServer.Name = "btnBrowseServer";
            this.btnBrowseServer.Size = new System.Drawing.Size(87, 26);
            this.btnBrowseServer.TabIndex = 2;
            this.btnBrowseServer.Text = "Browse...";
            this.btnBrowseServer.UseVisualStyleBackColor = false;
            this.btnBrowseServer.Click += new System.EventHandler(this.BtnBrowseServer_Click);
            // 
            // btnCompare
            // 
            this.btnCompare.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(150)))), ((int)(((byte)(136)))));
            this.btnCompare.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCompare.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCompare.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnCompare.ForeColor = System.Drawing.Color.White;
            this.btnCompare.Location = new System.Drawing.Point(253, 48);
            this.btnCompare.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.btnCompare.Name = "btnCompare";
            this.btnCompare.Size = new System.Drawing.Size(118, 40);
            this.btnCompare.TabIndex = 2;
            this.btnCompare.Text = "Compare";
            this.btnCompare.UseVisualStyleBackColor = false;
            this.btnCompare.Click += new System.EventHandler(this.BtnCompare_Click);
            // 
            // btnExport
            // 
            this.btnExport.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(212)))));
            this.btnExport.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnExport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExport.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnExport.ForeColor = System.Drawing.Color.White;
            this.btnExport.Location = new System.Drawing.Point(926, 48);
            this.btnExport.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(118, 40);
            this.btnExport.TabIndex = 3;
            this.btnExport.Text = "Export";
            this.btnExport.UseVisualStyleBackColor = false;
            this.btnExport.Enabled = false;
            this.btnExport.Click += new System.EventHandler(this.BtnExport_Click);
            // 
            // splitContainer
            // 
            this.mainLayout.SetColumnSpan(this.splitContainer, 6);
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(13, 91);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.treeResults);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.listDetails);
            this.splitContainer.Size = new System.Drawing.Size(1174, 546);
            this.splitContainer.SplitterDistance = 450;
            this.splitContainer.SplitterWidth = 5;
            this.splitContainer.TabIndex = 4;
            // 
            // treeResults
            // 
            this.treeResults.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeResults.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.treeResults.FullRowSelect = true;
            this.treeResults.HideSelection = false;
            this.treeResults.ImageIndex = 4;
            this.treeResults.ImageList = this.imageList;
            this.treeResults.Location = new System.Drawing.Point(0, 0);
            this.treeResults.Name = "treeResults";
            this.treeResults.SelectedImageIndex = 4;
            treeNode1.Name = "nodeReady";
            treeNode1.Text = "Ready to compare";
            this.treeResults.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1});
            this.treeResults.ShowNodeToolTips = true;
            this.treeResults.Size = new System.Drawing.Size(450, 546);
            this.treeResults.TabIndex = 0;
            this.treeResults.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.TreeResults_AfterSelect);
            // 
            // imageList
            // 
            this.imageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            this.imageList.ImageSize = new System.Drawing.Size(16, 16);
            this.imageList.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // listDetails
            // 
            this.listDetails.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listDetails.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colProperty,
            this.colOldValue,
            this.colNewValue});
            this.listDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listDetails.Font = new System.Drawing.Font("Consolas", 9.75F);
            this.listDetails.FullRowSelect = true;
            this.listDetails.GridLines = true;
            this.listDetails.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listDetails.Location = new System.Drawing.Point(0, 0);
            this.listDetails.MultiSelect = false;
            this.listDetails.Name = "listDetails";
            this.listDetails.Size = new System.Drawing.Size(719, 546);
            this.listDetails.TabIndex = 0;
            this.listDetails.UseCompatibleStateImageBehavior = false;
            this.listDetails.View = System.Windows.Forms.View.Details;
            // 
            // colProperty
            // 
            this.colProperty.Text = "Property";
            this.colProperty.Width = 200;
            // 
            // colOldValue
            // 
            this.colOldValue.Text = "Server (Old)";
            this.colOldValue.Width = 250;
            // 
            // colNewValue
            // 
            this.colNewValue.Text = "Local (New)";
            this.colNewValue.Width = 250;
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 650);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1200, 22);
            this.statusStrip.TabIndex = 1;
            this.statusStrip.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(226, 17);
            this.lblStatus.Text = "Ready. Select SQL files to compare.";
            // 
            // saveFileDialog
            // 
            this.saveFileDialog.DefaultExt = "sql";
            this.saveFileDialog.Filter = "SQL Files|*.sql|All Files|*.*";
            this.saveFileDialog.Title = "Export Changes As...";
            // 
            // openFileDialog
            // 
            this.openFileDialog.Filter = "SQL Files|*.sql|All Files|*.*";
            this.openFileDialog.Title = "Select SQL File";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1200, 672);
            this.Controls.Add(this.mainLayout);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SQL Compare Tool - Compare Local vs Server Schema";
            this.mainLayout.ResumeLayout(false);
            this.localPanel.ResumeLayout(false);
            this.localPanel.PerformLayout();
            this.serverPanel.ResumeLayout(false);
            this.serverPanel.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}

