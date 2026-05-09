namespace CLS_II
{
    partial class AdsSymbolSample
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.treeViewSymbols = new System.Windows.Forms.TreeView();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.tbHandle = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.tbName = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.tbIndexOffset = new System.Windows.Forms.TextBox();
            this.tbDatatype = new System.Windows.Forms.TextBox();
            this.tbSize = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblObject = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel4 = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblAmsNetID = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel6 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel7 = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblPort = new System.Windows.Forms.ToolStripStatusLabel();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.treeViewSymbols);
            this.groupBox1.Location = new System.Drawing.Point(12, 13);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Size = new System.Drawing.Size(412, 362);
            this.groupBox1.TabIndex = 32;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Symbols";
            // 
            // treeViewSymbols
            // 
            this.treeViewSymbols.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.treeViewSymbols.Location = new System.Drawing.Point(15, 29);
            this.treeViewSymbols.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.treeViewSymbols.Name = "treeViewSymbols";
            this.treeViewSymbols.Size = new System.Drawing.Size(384, 325);
            this.treeViewSymbols.TabIndex = 0;
            this.treeViewSymbols.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeViewSymbols_AfterSelect);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.tbHandle);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.button2);
            this.groupBox2.Controls.Add(this.button1);
            this.groupBox2.Controls.Add(this.tbName);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.tbIndexOffset);
            this.groupBox2.Controls.Add(this.tbDatatype);
            this.groupBox2.Controls.Add(this.tbSize);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label9);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Location = new System.Drawing.Point(430, 13);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox2.Size = new System.Drawing.Size(376, 362);
            this.groupBox2.TabIndex = 51;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "SymbolInfo";
            // 
            // tbHandle
            // 
            this.tbHandle.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbHandle.Location = new System.Drawing.Point(160, 181);
            this.tbHandle.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbHandle.Name = "tbHandle";
            this.tbHandle.ReadOnly = true;
            this.tbHandle.Size = new System.Drawing.Size(200, 27);
            this.tbHandle.TabIndex = 67;
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(16, 181);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(79, 27);
            this.label2.TabIndex = 68;
            this.label2.Text = "Handle:";
            // 
            // button2
            // 
            this.button2.AutoSize = true;
            this.button2.Location = new System.Drawing.Point(270, 314);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(90, 40);
            this.button2.TabIndex = 66;
            this.button2.Text = "Cancel";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button1
            // 
            this.button1.AutoSize = true;
            this.button1.Location = new System.Drawing.Point(160, 314);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(90, 40);
            this.button1.TabIndex = 65;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // tbName
            // 
            this.tbName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbName.Location = new System.Drawing.Point(160, 39);
            this.tbName.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbName.Name = "tbName";
            this.tbName.ReadOnly = true;
            this.tbName.Size = new System.Drawing.Size(200, 27);
            this.tbName.TabIndex = 63;
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(16, 39);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(113, 27);
            this.label7.TabIndex = 64;
            this.label7.Text = "Name:";
            // 
            // tbIndexOffset
            // 
            this.tbIndexOffset.AcceptsReturn = true;
            this.tbIndexOffset.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbIndexOffset.Location = new System.Drawing.Point(160, 227);
            this.tbIndexOffset.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbIndexOffset.Multiline = true;
            this.tbIndexOffset.Name = "tbIndexOffset";
            this.tbIndexOffset.ReadOnly = true;
            this.tbIndexOffset.Size = new System.Drawing.Size(200, 80);
            this.tbIndexOffset.TabIndex = 50;
            // 
            // tbDatatype
            // 
            this.tbDatatype.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbDatatype.Location = new System.Drawing.Point(160, 84);
            this.tbDatatype.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbDatatype.Name = "tbDatatype";
            this.tbDatatype.ReadOnly = true;
            this.tbDatatype.Size = new System.Drawing.Size(200, 27);
            this.tbDatatype.TabIndex = 56;
            // 
            // tbSize
            // 
            this.tbSize.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbSize.Location = new System.Drawing.Point(160, 133);
            this.tbSize.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tbSize.Name = "tbSize";
            this.tbSize.ReadOnly = true;
            this.tbSize.Size = new System.Drawing.Size(200, 27);
            this.tbSize.TabIndex = 54;
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(16, 84);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(104, 27);
            this.label3.TabIndex = 57;
            this.label3.Text = "Datatype:";
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(16, 133);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(79, 27);
            this.label9.TabIndex = 55;
            this.label9.Text = "Size:";
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(16, 227);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(129, 43);
            this.label1.TabIndex = 51;
            this.label1.Text = "Comment:";
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.lblObject,
            this.toolStripStatusLabel3,
            this.toolStripStatusLabel4,
            this.lblAmsNetID,
            this.toolStripStatusLabel6,
            this.toolStripStatusLabel7,
            this.lblPort});
            this.statusStrip1.Location = new System.Drawing.Point(0, 388);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(817, 25);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 52;
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
            // toolStripStatusLabel6
            // 
            this.toolStripStatusLabel6.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.toolStripStatusLabel6.Name = "toolStripStatusLabel6";
            this.toolStripStatusLabel6.Size = new System.Drawing.Size(13, 20);
            this.toolStripStatusLabel6.Text = "|";
            // 
            // toolStripStatusLabel7
            // 
            this.toolStripStatusLabel7.Name = "toolStripStatusLabel7";
            this.toolStripStatusLabel7.Size = new System.Drawing.Size(48, 20);
            this.toolStripStatusLabel7.Text = "Port :";
            // 
            // lblPort
            // 
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(75, 20);
            this.lblPort.Text = "(Not Set)";
            // 
            // AdsSymbolSample
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(817, 413);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "AdsSymbolSample";
            this.ShowIcon = false;
            this.Text = "AdsSymbol Browser";
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TreeView treeViewSymbols;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox tbName;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbIndexOffset;
        private System.Windows.Forms.TextBox tbDatatype;
        private System.Windows.Forms.TextBox tbSize;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel lblObject;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel3;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel4;
        private System.Windows.Forms.ToolStripStatusLabel lblAmsNetID;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel6;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel7;
        private System.Windows.Forms.ToolStripStatusLabel lblPort;
        private System.Windows.Forms.TextBox tbHandle;
        private System.Windows.Forms.Label label2;
    }
}