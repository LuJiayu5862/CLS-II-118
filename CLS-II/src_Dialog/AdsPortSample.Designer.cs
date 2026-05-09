namespace CLS_II
{
    partial class AdsPortSample
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblObject = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel4 = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblAmsNetID = new System.Windows.Forms.ToolStripStatusLabel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.tbPortID = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.tbName = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.treeViewSymbols = new System.Windows.Forms.TreeView();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxRange1B = new System.Windows.Forms.TextBox();
            this.tbxRange2B = new System.Windows.Forms.TextBox();
            this.btnSearch = new System.Windows.Forms.Button();
            this.tbxRange1A = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxRange2A = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.statusStrip1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.lblObject,
            this.toolStripStatusLabel3,
            this.toolStripStatusLabel4,
            this.lblAmsNetID});
            this.statusStrip1.Location = new System.Drawing.Point(0, 356);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 16, 0);
            this.statusStrip1.Size = new System.Drawing.Size(766, 25);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 53;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(66, 20);
            this.toolStripStatusLabel1.Text = "Object :";
            // 
            // lblObject
            // 
            this.lblObject.Name = "lblObject";
            this.lblObject.Size = new System.Drawing.Size(115, 20);
            this.lblObject.Text = "(Not Selected)";
            // 
            // toolStripStatusLabel3
            // 
            this.toolStripStatusLabel3.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new System.Drawing.Size(13, 20);
            this.toolStripStatusLabel3.Text = "|";
            // 
            // toolStripStatusLabel4
            // 
            this.toolStripStatusLabel4.Name = "toolStripStatusLabel4";
            this.toolStripStatusLabel4.Size = new System.Drawing.Size(91, 20);
            this.toolStripStatusLabel4.Text = "AmsNetID :";
            // 
            // lblAmsNetID
            // 
            this.lblAmsNetID.Name = "lblAmsNetID";
            this.lblAmsNetID.Size = new System.Drawing.Size(75, 20);
            this.lblAmsNetID.Text = "(Not Set)";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnCancel);
            this.groupBox2.Controls.Add(this.btnOK);
            this.groupBox2.Controls.Add(this.tbPortID);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.tbName);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Location = new System.Drawing.Point(430, 190);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox2.Size = new System.Drawing.Size(325, 160);
            this.groupBox2.TabIndex = 55;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "PortInfo";
            // 
            // btnCancel
            // 
            this.btnCancel.AutoSize = true;
            this.btnCancel.Location = new System.Drawing.Point(220, 110);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 40);
            this.btnCancel.TabIndex = 66;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.AutoSize = true;
            this.btnOK.Location = new System.Drawing.Point(107, 110);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(90, 40);
            this.btnOK.TabIndex = 65;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // tbPortID
            // 
            this.tbPortID.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbPortID.Location = new System.Drawing.Point(109, 29);
            this.tbPortID.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbPortID.Name = "tbPortID";
            this.tbPortID.ReadOnly = true;
            this.tbPortID.Size = new System.Drawing.Size(200, 27);
            this.tbPortID.TabIndex = 63;
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(18, 29);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(61, 27);
            this.label7.TabIndex = 64;
            this.label7.Text = "PortID:";
            // 
            // tbName
            // 
            this.tbName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbName.Location = new System.Drawing.Point(109, 74);
            this.tbName.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbName.Name = "tbName";
            this.tbName.ReadOnly = true;
            this.tbName.Size = new System.Drawing.Size(200, 27);
            this.tbName.TabIndex = 56;
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(18, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(61, 27);
            this.label3.TabIndex = 57;
            this.label3.Text = "Name:";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.treeViewSymbols);
            this.groupBox1.Location = new System.Drawing.Point(12, 13);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Size = new System.Drawing.Size(412, 337);
            this.groupBox1.TabIndex = 54;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Ports";
            // 
            // treeViewSymbols
            // 
            this.treeViewSymbols.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.treeViewSymbols.Location = new System.Drawing.Point(15, 29);
            this.treeViewSymbols.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.treeViewSymbols.Name = "treeViewSymbols";
            this.treeViewSymbols.Size = new System.Drawing.Size(384, 298);
            this.treeViewSymbols.TabIndex = 0;
            this.treeViewSymbols.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeViewSymbols_AfterSelect);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label4);
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.tbxRange1B);
            this.groupBox3.Controls.Add(this.tbxRange2B);
            this.groupBox3.Controls.Add(this.btnSearch);
            this.groupBox3.Controls.Add(this.tbxRange1A);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.tbxRange2A);
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Location = new System.Drawing.Point(430, 13);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox3.Size = new System.Drawing.Size(325, 169);
            this.groupBox3.TabIndex = 56;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Search";
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(197, 31);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(24, 27);
            this.label4.TabIndex = 72;
            this.label4.Text = "--";
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(197, 76);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(24, 27);
            this.label5.TabIndex = 71;
            this.label5.Text = "--";
            // 
            // tbxRange1B
            // 
            this.tbxRange1B.BackColor = System.Drawing.SystemColors.Window;
            this.tbxRange1B.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbxRange1B.Location = new System.Drawing.Point(227, 29);
            this.tbxRange1B.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbxRange1B.Name = "tbxRange1B";
            this.tbxRange1B.Size = new System.Drawing.Size(81, 27);
            this.tbxRange1B.TabIndex = 68;
            this.tbxRange1B.Text = "365";
            this.tbxRange1B.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbxRange1B.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tbxRange1A_KeyPress);
            // 
            // tbxRange2B
            // 
            this.tbxRange2B.BackColor = System.Drawing.SystemColors.Window;
            this.tbxRange2B.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbxRange2B.Location = new System.Drawing.Point(227, 74);
            this.tbxRange2B.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbxRange2B.Name = "tbxRange2B";
            this.tbxRange2B.Size = new System.Drawing.Size(81, 27);
            this.tbxRange2B.TabIndex = 67;
            this.tbxRange2B.Text = "855";
            this.tbxRange2B.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbxRange2B.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tbxRange1A_KeyPress);
            // 
            // btnSearch
            // 
            this.btnSearch.AutoSize = true;
            this.btnSearch.Location = new System.Drawing.Point(219, 117);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(90, 40);
            this.btnSearch.TabIndex = 66;
            this.btnSearch.Text = "Search";
            this.btnSearch.UseVisualStyleBackColor = true;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // tbxRange1A
            // 
            this.tbxRange1A.BackColor = System.Drawing.SystemColors.Window;
            this.tbxRange1A.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbxRange1A.ForeColor = System.Drawing.SystemColors.WindowText;
            this.tbxRange1A.Location = new System.Drawing.Point(109, 29);
            this.tbxRange1A.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbxRange1A.Name = "tbxRange1A";
            this.tbxRange1A.Size = new System.Drawing.Size(82, 27);
            this.tbxRange1A.TabIndex = 63;
            this.tbxRange1A.Text = "350";
            this.tbxRange1A.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbxRange1A.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tbxRange1A_KeyPress);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(18, 29);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(85, 27);
            this.label1.TabIndex = 64;
            this.label1.Text = "Range1:";
            // 
            // tbxRange2A
            // 
            this.tbxRange2A.BackColor = System.Drawing.SystemColors.Window;
            this.tbxRange2A.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbxRange2A.Location = new System.Drawing.Point(109, 74);
            this.tbxRange2A.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbxRange2A.Name = "tbxRange2A";
            this.tbxRange2A.Size = new System.Drawing.Size(82, 27);
            this.tbxRange2A.TabIndex = 56;
            this.tbxRange2A.Text = "851";
            this.tbxRange2A.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbxRange2A.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tbxRange1A_KeyPress);
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(18, 74);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(85, 27);
            this.label2.TabIndex = 57;
            this.label2.Text = "Range2:";
            // 
            // AdsPortSample
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(766, 381);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.statusStrip1);
            this.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "AdsPortSample";
            this.ShowIcon = false;
            this.Text = "AdsPort Browser";
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel lblObject;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel3;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel4;
        private System.Windows.Forms.ToolStripStatusLabel lblAmsNetID;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.TextBox tbPortID;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbName;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TreeView treeViewSymbols;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxRange1B;
        private System.Windows.Forms.TextBox tbxRange2B;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.TextBox tbxRange1A;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxRange2A;
        private System.Windows.Forms.Label label2;
    }
}