namespace GpsCycleWin32
{
    partial class FormWin32
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.button1 = new System.Windows.Forms.Button();
            this.label9 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.buttonLoadMaps = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.comboBoxLine2OptWidth = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.comboBoxKmlOptWidth = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.comboMultiMaps = new System.Windows.Forms.ComboBox();
            this.checkIsRunning = new System.Windows.Forms.CheckBox();
            this.checkPlotLine2AsDots = new System.Windows.Forms.CheckBox();
            this.checkPlotTrackAsDots = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.comboBoxLine2OptColor = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboBoxKmlOptColor = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboUnits = new System.Windows.Forms.ComboBox();
            this.buttonLoadKml2 = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.textLoadKml2 = new System.Windows.Forms.TextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.buttonZoomOut = new System.Windows.Forms.Button();
            this.buttonZoomIn = new System.Windows.Forms.Button();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(852, 854);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.button1);
            this.tabPage1.Controls.Add(this.label9);
            this.tabPage1.Controls.Add(this.textBox1);
            this.tabPage1.Controls.Add(this.buttonLoadMaps);
            this.tabPage1.Controls.Add(this.label8);
            this.tabPage1.Controls.Add(this.label7);
            this.tabPage1.Controls.Add(this.comboBoxLine2OptWidth);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.comboBoxKmlOptWidth);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.comboMultiMaps);
            this.tabPage1.Controls.Add(this.checkIsRunning);
            this.tabPage1.Controls.Add(this.checkPlotLine2AsDots);
            this.tabPage1.Controls.Add(this.checkPlotTrackAsDots);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Controls.Add(this.comboBoxLine2OptColor);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.comboBoxKmlOptColor);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.comboUnits);
            this.tabPage1.Controls.Add(this.buttonLoadKml2);
            this.tabPage1.Controls.Add(this.Label1);
            this.tabPage1.Controls.Add(this.textLoadKml2);
            this.tabPage1.Location = new System.Drawing.Point(4, 25);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPage1.Size = new System.Drawing.Size(844, 825);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "tabPage1";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(29, 279);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(120, 23);
            this.button1.TabIndex = 22;
            this.button1.Text = "Load KML 2";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(47, 222);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(250, 16);
            this.label9.TabIndex = 21;
            this.label9.Text = "Input map folder it C:\\temp\\osm_read\\tiles";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(426, 138);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 22);
            this.textBox1.TabIndex = 20;
            this.textBox1.Text = "0.0";
            // 
            // buttonLoadMaps
            // 
            this.buttonLoadMaps.Location = new System.Drawing.Point(293, 138);
            this.buttonLoadMaps.Name = "buttonLoadMaps";
            this.buttonLoadMaps.Size = new System.Drawing.Size(120, 23);
            this.buttonLoadMaps.TabIndex = 19;
            this.buttonLoadMaps.Text = "Load Maps";
            this.buttonLoadMaps.UseVisualStyleBackColor = true;
            this.buttonLoadMaps.Click += new System.EventHandler(this.buttonLoadMaps_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(47, 138);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(130, 16);
            this.label8.TabIndex = 18;
            this.label8.Text = "Input folder it c:\\temp";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(205, 86);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(42, 16);
            this.label7.TabIndex = 17;
            this.label7.Text = "Width";
            // 
            // comboBoxLine2OptWidth
            // 
            this.comboBoxLine2OptWidth.FormattingEnabled = true;
            this.comboBoxLine2OptWidth.Items.AddRange(new object[] {
            "2",
            "4",
            "6",
            "8",
            "10",
            "12",
            "14",
            "16"});
            this.comboBoxLine2OptWidth.Location = new System.Drawing.Point(262, 83);
            this.comboBoxLine2OptWidth.Name = "comboBoxLine2OptWidth";
            this.comboBoxLine2OptWidth.Size = new System.Drawing.Size(131, 24);
            this.comboBoxLine2OptWidth.TabIndex = 16;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(205, 56);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(42, 16);
            this.label6.TabIndex = 15;
            this.label6.Text = "Width";
            // 
            // comboBoxKmlOptWidth
            // 
            this.comboBoxKmlOptWidth.FormattingEnabled = true;
            this.comboBoxKmlOptWidth.Items.AddRange(new object[] {
            "2",
            "4",
            "6",
            "8",
            "10",
            "12",
            "14",
            "16"});
            this.comboBoxKmlOptWidth.Location = new System.Drawing.Point(262, 53);
            this.comboBoxKmlOptWidth.Name = "comboBoxKmlOptWidth";
            this.comboBoxKmlOptWidth.Size = new System.Drawing.Size(131, 24);
            this.comboBoxKmlOptWidth.TabIndex = 14;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(220, 19);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(68, 16);
            this.label5.TabIndex = 13;
            this.label5.Text = "Multimaps";
            // 
            // comboMultiMaps
            // 
            this.comboMultiMaps.FormattingEnabled = true;
            this.comboMultiMaps.Items.AddRange(new object[] {
            "off",
            "2 maps, 1x zoom",
            "2 maps, 2x zoom",
            "2 maps, 4x zoom",
            "3 maps, 1x zoom",
            "3 maps, 2x zoom",
            "3 maps, 4x zoom",
            "4 maps, 1x zoom",
            "4 maps, 2x zoom",
            "4 maps, 4x zoom",
            "6 maps, 1x zoom",
            "6 maps, 2x zoom",
            "6 maps, 4x zoom",
            "8 maps, 1x zoom",
            "8 maps, 2x zoom",
            "8 maps, 4x zoom"});
            this.comboMultiMaps.Location = new System.Drawing.Point(304, 16);
            this.comboMultiMaps.Name = "comboMultiMaps";
            this.comboMultiMaps.Size = new System.Drawing.Size(131, 24);
            this.comboMultiMaps.TabIndex = 12;
            // 
            // checkIsRunning
            // 
            this.checkIsRunning.AutoSize = true;
            this.checkIsRunning.Location = new System.Drawing.Point(462, 18);
            this.checkIsRunning.Name = "checkIsRunning";
            this.checkIsRunning.Size = new System.Drawing.Size(88, 20);
            this.checkIsRunning.TabIndex = 11;
            this.checkIsRunning.Text = "Is Running";
            this.checkIsRunning.UseVisualStyleBackColor = true;
            // 
            // checkPlotLine2AsDots
            // 
            this.checkPlotLine2AsDots.AutoSize = true;
            this.checkPlotLine2AsDots.Location = new System.Drawing.Point(426, 84);
            this.checkPlotLine2AsDots.Name = "checkPlotLine2AsDots";
            this.checkPlotLine2AsDots.Size = new System.Drawing.Size(110, 20);
            this.checkPlotLine2AsDots.TabIndex = 10;
            this.checkPlotLine2AsDots.Text = "Track As Dots";
            this.checkPlotLine2AsDots.UseVisualStyleBackColor = true;
            // 
            // checkPlotTrackAsDots
            // 
            this.checkPlotTrackAsDots.AutoSize = true;
            this.checkPlotTrackAsDots.Location = new System.Drawing.Point(426, 55);
            this.checkPlotTrackAsDots.Name = "checkPlotTrackAsDots";
            this.checkPlotTrackAsDots.Size = new System.Drawing.Size(110, 20);
            this.checkPlotTrackAsDots.TabIndex = 9;
            this.checkPlotTrackAsDots.Text = "Track As Dots";
            this.checkPlotTrackAsDots.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(10, 83);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(47, 16);
            this.label4.TabIndex = 8;
            this.label4.Text = "Track2";
            // 
            // comboBoxLine2OptColor
            // 
            this.comboBoxLine2OptColor.FormattingEnabled = true;
            this.comboBoxLine2OptColor.Items.AddRange(new object[] {
            "blue",
            "red",
            "green",
            "yellow",
            "white",
            "black",
            "gray"});
            this.comboBoxLine2OptColor.Location = new System.Drawing.Point(67, 80);
            this.comboBoxLine2OptColor.Name = "comboBoxLine2OptColor";
            this.comboBoxLine2OptColor.Size = new System.Drawing.Size(131, 24);
            this.comboBoxLine2OptColor.TabIndex = 7;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 54);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 16);
            this.label3.TabIndex = 6;
            this.label3.Text = "Track1";
            // 
            // comboBoxKmlOptColor
            // 
            this.comboBoxKmlOptColor.FormattingEnabled = true;
            this.comboBoxKmlOptColor.Items.AddRange(new object[] {
            "blue",
            "red",
            "green",
            "yellow",
            "white",
            "black",
            "gray"});
            this.comboBoxKmlOptColor.Location = new System.Drawing.Point(67, 51);
            this.comboBoxKmlOptColor.Name = "comboBoxKmlOptColor";
            this.comboBoxKmlOptColor.Size = new System.Drawing.Size(131, 24);
            this.comboBoxKmlOptColor.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 24);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(38, 16);
            this.label2.TabIndex = 4;
            this.label2.Text = "Units";
            // 
            // comboUnits
            // 
            this.comboUnits.FormattingEnabled = true;
            this.comboUnits.Items.AddRange(new object[] {
            "miles / mph",
            "km / kmh",
            "naut miles / knots",
            "miles / mph / ft",
            "km / min per km",
            "miles / min per mile / ft"});
            this.comboUnits.Location = new System.Drawing.Point(67, 21);
            this.comboUnits.Name = "comboUnits";
            this.comboUnits.Size = new System.Drawing.Size(131, 24);
            this.comboUnits.TabIndex = 3;
            // 
            // buttonLoadKml2
            // 
            this.buttonLoadKml2.Location = new System.Drawing.Point(293, 173);
            this.buttonLoadKml2.Name = "buttonLoadKml2";
            this.buttonLoadKml2.Size = new System.Drawing.Size(120, 23);
            this.buttonLoadKml2.TabIndex = 2;
            this.buttonLoadKml2.Text = "Load KML 2";
            this.buttonLoadKml2.UseVisualStyleBackColor = true;
            this.buttonLoadKml2.Click += new System.EventHandler(this.buttonLoadKml2_Click);
            // 
            // Label1
            // 
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(26, 173);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(49, 16);
            this.Label1.TabIndex = 1;
            this.Label1.Text = "KML_2";
            // 
            // textLoadKml2
            // 
            this.textLoadKml2.Location = new System.Drawing.Point(83, 173);
            this.textLoadKml2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textLoadKml2.Name = "textLoadKml2";
            this.textLoadKml2.Size = new System.Drawing.Size(194, 22);
            this.textLoadKml2.TabIndex = 0;
            this.textLoadKml2.Text = "chip_test1.kml";
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.buttonZoomOut);
            this.tabPage2.Controls.Add(this.buttonZoomIn);
            this.tabPage2.Location = new System.Drawing.Point(4, 25);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPage2.Size = new System.Drawing.Size(844, 825);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Graph";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // buttonZoomOut
            // 
            this.buttonZoomOut.Location = new System.Drawing.Point(392, 7);
            this.buttonZoomOut.Name = "buttonZoomOut";
            this.buttonZoomOut.Size = new System.Drawing.Size(75, 23);
            this.buttonZoomOut.TabIndex = 4;
            this.buttonZoomOut.Text = "Zoom Out";
            this.buttonZoomOut.UseVisualStyleBackColor = true;
            this.buttonZoomOut.Click += new System.EventHandler(this.buttonZoomOut_Click);
            // 
            // buttonZoomIn
            // 
            this.buttonZoomIn.Location = new System.Drawing.Point(311, 7);
            this.buttonZoomIn.Name = "buttonZoomIn";
            this.buttonZoomIn.Size = new System.Drawing.Size(75, 23);
            this.buttonZoomIn.TabIndex = 3;
            this.buttonZoomIn.Text = "Zoom In";
            this.buttonZoomIn.UseVisualStyleBackColor = true;
            this.buttonZoomIn.Click += new System.EventHandler(this.buttonZoomIn_Click);
            // 
            // tabPage3
            // 
            this.tabPage3.Location = new System.Drawing.Point(4, 25);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPage3.Size = new System.Drawing.Size(844, 825);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "tabPage3";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // FormWin32
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(852, 854);
            this.Controls.Add(this.tabControl1);
            this.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "FormWin32";
            this.Text = "Form1";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TextBox textLoadKml2;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Button buttonLoadKml2;
        private System.Windows.Forms.Label Label1;
        private System.Windows.Forms.CheckBox checkPlotLine2AsDots;
        private System.Windows.Forms.CheckBox checkPlotTrackAsDots;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboBoxLine2OptColor;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox comboBoxKmlOptColor;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboUnits;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboMultiMaps;
        private System.Windows.Forms.CheckBox checkIsRunning;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox comboBoxLine2OptWidth;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox comboBoxKmlOptWidth;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button buttonLoadMaps;
        private System.Windows.Forms.Button buttonZoomOut;
        private System.Windows.Forms.Button buttonZoomIn;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button button1;
    }
}

