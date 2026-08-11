using System.ComponentModel.Design;

namespace SysBot.Pokemon
{
    partial class EggDetailForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EggDetailForm));
            PokemonText = new System.Windows.Forms.TextBox();
            PokePic = new System.Windows.Forms.PictureBox();
            MarkPic = new System.Windows.Forms.PictureBox();
            ShinyPic = new System.Windows.Forms.PictureBox();
            EggGroup = new System.Windows.Forms.GroupBox();
            ParentGroup = new System.Windows.Forms.GroupBox();
            P1PokePic = new System.Windows.Forms.PictureBox();
            ParentOneText = new System.Windows.Forms.TextBox();
            P1MarkPic = new System.Windows.Forms.PictureBox();
            P1ShinyPic = new System.Windows.Forms.PictureBox();
            P2PokePic = new System.Windows.Forms.PictureBox();
            ParentTwoText = new System.Windows.Forms.TextBox();
            P2MarkPic = new System.Windows.Forms.PictureBox();
            P2ShinyPic = new System.Windows.Forms.PictureBox();
            P1Label = new System.Windows.Forms.Label();
            P2Label = new System.Windows.Forms.Label();
            EggLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)PokePic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)MarkPic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ShinyPic).BeginInit();
            EggGroup.SuspendLayout();
            ParentGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)P1PokePic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)P1MarkPic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)P1ShinyPic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)P2PokePic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)P2MarkPic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)P2ShinyPic).BeginInit();
            SuspendLayout();
            // 
            // PokemonText
            // 
            PokemonText.BackColor = System.Drawing.SystemColors.Control;
            PokemonText.Font = new System.Drawing.Font("Yu Gothic UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 128);
            PokemonText.Location = new System.Drawing.Point(19, 250);
            PokemonText.Multiline = true;
            PokemonText.Name = "PokemonText";
            PokemonText.ReadOnly = true;
            PokemonText.Size = new System.Drawing.Size(380, 300);
            PokemonText.TabIndex = 0;
            PokemonText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // PokePic
            // 
            PokePic.Location = new System.Drawing.Point(19, 54);
            PokePic.Name = "PokePic";
            PokePic.Size = new System.Drawing.Size(190, 190);
            PokePic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            PokePic.TabIndex = 1;
            PokePic.TabStop = false;
            // 
            // MarkPic
            // 
            MarkPic.Location = new System.Drawing.Point(262, 149);
            MarkPic.Name = "MarkPic";
            MarkPic.Size = new System.Drawing.Size(95, 95);
            MarkPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            MarkPic.TabIndex = 2;
            MarkPic.TabStop = false;
            // 
            // ShinyPic
            // 
            ShinyPic.Location = new System.Drawing.Point(262, 54);
            ShinyPic.Name = "ShinyPic";
            ShinyPic.Size = new System.Drawing.Size(55, 55);
            ShinyPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            ShinyPic.TabIndex = 4;
            ShinyPic.TabStop = false;
            // 
            // EggGroup
            // 
            EggGroup.Controls.Add(EggLabel);
            EggGroup.Controls.Add(PokePic);
            EggGroup.Controls.Add(PokemonText);
            EggGroup.Controls.Add(MarkPic);
            EggGroup.Controls.Add(ShinyPic);
            EggGroup.Location = new System.Drawing.Point(12, 12);
            EggGroup.Name = "EggGroup";
            EggGroup.Size = new System.Drawing.Size(414, 615);
            EggGroup.TabIndex = 5;
            EggGroup.TabStop = false;
            EggGroup.Text = "Egg Group";
            // 
            // ParentGroup
            // 
            ParentGroup.Controls.Add(P2Label);
            ParentGroup.Controls.Add(P1Label);
            ParentGroup.Controls.Add(P2PokePic);
            ParentGroup.Controls.Add(ParentTwoText);
            ParentGroup.Controls.Add(P2MarkPic);
            ParentGroup.Controls.Add(P2ShinyPic);
            ParentGroup.Controls.Add(P1PokePic);
            ParentGroup.Controls.Add(ParentOneText);
            ParentGroup.Controls.Add(P1MarkPic);
            ParentGroup.Controls.Add(P1ShinyPic);
            ParentGroup.Location = new System.Drawing.Point(432, 12);
            ParentGroup.Name = "ParentGroup";
            ParentGroup.Size = new System.Drawing.Size(223, 615);
            ParentGroup.TabIndex = 6;
            ParentGroup.TabStop = false;
            ParentGroup.Text = "Parents Group";
            // 
            // P1PokePic
            // 
            P1PokePic.Location = new System.Drawing.Point(19, 54);
            P1PokePic.Name = "P1PokePic";
            P1PokePic.Size = new System.Drawing.Size(95, 95);
            P1PokePic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            P1PokePic.TabIndex = 1;
            P1PokePic.TabStop = false;
            // 
            // ParentOneText
            // 
            ParentOneText.BackColor = System.Drawing.SystemColors.Control;
            ParentOneText.Font = new System.Drawing.Font("Yu Gothic UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 128);
            ParentOneText.Location = new System.Drawing.Point(19, 155);
            ParentOneText.Multiline = true;
            ParentOneText.Name = "ParentOneText";
            ParentOneText.ReadOnly = true;
            ParentOneText.Size = new System.Drawing.Size(190, 150);
            ParentOneText.TabIndex = 0;
            ParentOneText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // P1MarkPic
            // 
            P1MarkPic.Location = new System.Drawing.Point(134, 101);
            P1MarkPic.Name = "P1MarkPic";
            P1MarkPic.Size = new System.Drawing.Size(48, 48);
            P1MarkPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            P1MarkPic.TabIndex = 2;
            P1MarkPic.TabStop = false;
            // 
            // P1ShinyPic
            // 
            P1ShinyPic.Location = new System.Drawing.Point(134, 54);
            P1ShinyPic.Name = "P1ShinyPic";
            P1ShinyPic.Size = new System.Drawing.Size(28, 28);
            P1ShinyPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            P1ShinyPic.TabIndex = 4;
            P1ShinyPic.TabStop = false;
            // 
            // P2PokePic
            // 
            P2PokePic.Location = new System.Drawing.Point(19, 354);
            P2PokePic.Name = "P2PokePic";
            P2PokePic.Size = new System.Drawing.Size(95, 95);
            P2PokePic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            P2PokePic.TabIndex = 6;
            P2PokePic.TabStop = false;
            // 
            // ParentTwoText
            // 
            ParentTwoText.BackColor = System.Drawing.SystemColors.Control;
            ParentTwoText.Font = new System.Drawing.Font("Yu Gothic UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 128);
            ParentTwoText.Location = new System.Drawing.Point(19, 455);
            ParentTwoText.Multiline = true;
            ParentTwoText.Name = "ParentTwoText";
            ParentTwoText.ReadOnly = true;
            ParentTwoText.Size = new System.Drawing.Size(190, 150);
            ParentTwoText.TabIndex = 5;
            ParentTwoText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // P2MarkPic
            // 
            P2MarkPic.Location = new System.Drawing.Point(134, 401);
            P2MarkPic.Name = "P2MarkPic";
            P2MarkPic.Size = new System.Drawing.Size(48, 48);
            P2MarkPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            P2MarkPic.TabIndex = 7;
            P2MarkPic.TabStop = false;
            // 
            // P2ShinyPic
            // 
            P2ShinyPic.Location = new System.Drawing.Point(134, 354);
            P2ShinyPic.Name = "P2ShinyPic";
            P2ShinyPic.Size = new System.Drawing.Size(28, 28);
            P2ShinyPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            P2ShinyPic.TabIndex = 8;
            P2ShinyPic.TabStop = false;
            // 
            // P1Label
            // 
            P1Label.AutoSize = true;
            P1Label.Location = new System.Drawing.Point(19, 19);
            P1Label.Name = "P1Label";
            P1Label.Size = new System.Drawing.Size(74, 15);
            P1Label.TabIndex = 9;
            P1Label.Text = "Parent1 Data";
            // 
            // P2Label
            // 
            P2Label.AutoSize = true;
            P2Label.Location = new System.Drawing.Point(19, 325);
            P2Label.Name = "P2Label";
            P2Label.Size = new System.Drawing.Size(74, 15);
            P2Label.TabIndex = 10;
            P2Label.Text = "Parent2 Data";
            // 
            // EggLabel
            // 
            EggLabel.AutoSize = true;
            EggLabel.Location = new System.Drawing.Point(19, 19);
            EggLabel.Name = "EggLabel";
            EggLabel.Size = new System.Drawing.Size(54, 15);
            EggLabel.TabIndex = 11;
            EggLabel.Text = "Egg Data";
            // 
            // EggDetailForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.HighlightText;
            ClientSize = new System.Drawing.Size(667, 639);
            Controls.Add(ParentGroup);
            Controls.Add(EggGroup);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "EggDetailForm";
            Text = "Egg Detail";
            ((System.ComponentModel.ISupportInitialize)PokePic).EndInit();
            ((System.ComponentModel.ISupportInitialize)MarkPic).EndInit();
            ((System.ComponentModel.ISupportInitialize)ShinyPic).EndInit();
            EggGroup.ResumeLayout(false);
            EggGroup.PerformLayout();
            ParentGroup.ResumeLayout(false);
            ParentGroup.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)P1PokePic).EndInit();
            ((System.ComponentModel.ISupportInitialize)P1MarkPic).EndInit();
            ((System.ComponentModel.ISupportInitialize)P1ShinyPic).EndInit();
            ((System.ComponentModel.ISupportInitialize)P2PokePic).EndInit();
            ((System.ComponentModel.ISupportInitialize)P2MarkPic).EndInit();
            ((System.ComponentModel.ISupportInitialize)P2ShinyPic).EndInit();
            ResumeLayout(false);
        }
        #endregion

        private System.Windows.Forms.TextBox PokemonText;
        private System.Windows.Forms.PictureBox PokePic;
        private System.Windows.Forms.PictureBox MarkPic;
        private System.Windows.Forms.PictureBox ShinyPic;
        private System.Windows.Forms.GroupBox EggGroup;
        private System.Windows.Forms.GroupBox ParentGroup;
        private System.Windows.Forms.PictureBox P1PokePic;
        private System.Windows.Forms.TextBox ParentOneText;
        private System.Windows.Forms.PictureBox P1MarkPic;
        private System.Windows.Forms.PictureBox P1ShinyPic;
        private System.Windows.Forms.Label P2Label;
        private System.Windows.Forms.Label P1Label;
        private System.Windows.Forms.PictureBox P2PokePic;
        private System.Windows.Forms.TextBox ParentTwoText;
        private System.Windows.Forms.PictureBox P2MarkPic;
        private System.Windows.Forms.PictureBox P2ShinyPic;
        private System.Windows.Forms.Label EggLabel;
    }
}
