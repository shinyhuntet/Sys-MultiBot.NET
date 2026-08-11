using System.ComponentModel.Design;

namespace SysBot.Pokemon
{
    partial class PokeDetailForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PokeDetailForm));
            PokemonText = new System.Windows.Forms.TextBox();
            PokePic = new System.Windows.Forms.PictureBox();
            MarkPic = new System.Windows.Forms.PictureBox();
            TypePic = new System.Windows.Forms.PictureBox();
            ShinyPic = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)PokePic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)MarkPic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)TypePic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ShinyPic).BeginInit();
            SuspendLayout();
            // 
            // PokemonText
            // 
            PokemonText.BackColor = System.Drawing.SystemColors.Control;
            PokemonText.Font = new System.Drawing.Font("Yu Gothic UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 128);
            PokemonText.Location = new System.Drawing.Point(21, 239);
            PokemonText.Multiline = true;
            PokemonText.Name = "PokemonText";
            PokemonText.ReadOnly = true;
            PokemonText.Size = new System.Drawing.Size(377, 300);
            PokemonText.TabIndex = 0;
            PokemonText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // PokePic
            // 
            PokePic.Location = new System.Drawing.Point(21, 31);
            PokePic.Name = "PokePic";
            PokePic.Size = new System.Drawing.Size(190, 190);
            PokePic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            PokePic.TabIndex = 1;
            PokePic.TabStop = false;
            // 
            // MarkPic
            // 
            MarkPic.Location = new System.Drawing.Point(242, 125);
            MarkPic.Name = "MarkPic";
            MarkPic.Size = new System.Drawing.Size(95, 95);
            MarkPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            MarkPic.TabIndex = 2;
            MarkPic.TabStop = false;
            // 
            // TypePic
            // 
            TypePic.Location = new System.Drawing.Point(242, 24);
            TypePic.Name = "TypePic";
            TypePic.Size = new System.Drawing.Size(95, 95);
            TypePic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            TypePic.TabIndex = 3;
            TypePic.TabStop = false;
            // 
            // ShinyPic
            // 
            ShinyPic.Location = new System.Drawing.Point(343, 165);
            ShinyPic.Name = "ShinyPic";
            ShinyPic.Size = new System.Drawing.Size(55, 55);
            ShinyPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            ShinyPic.TabIndex = 4;
            ShinyPic.TabStop = false;
            // 
            // PokeDetailForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.HighlightText;
            ClientSize = new System.Drawing.Size(429, 551);
            Controls.Add(ShinyPic);
            Controls.Add(TypePic);
            Controls.Add(MarkPic);
            Controls.Add(PokePic);
            Controls.Add(PokemonText);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "PokeDetailForm";
            Text = "Poke Detail";
            ((System.ComponentModel.ISupportInitialize)PokePic).EndInit();
            ((System.ComponentModel.ISupportInitialize)MarkPic).EndInit();
            ((System.ComponentModel.ISupportInitialize)TypePic).EndInit();
            ((System.ComponentModel.ISupportInitialize)ShinyPic).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion

        private System.Windows.Forms.TextBox PokemonText;
        private System.Windows.Forms.PictureBox PokePic;
        private System.Windows.Forms.PictureBox MarkPic;
        private System.Windows.Forms.PictureBox TypePic;
        private System.Windows.Forms.PictureBox ShinyPic;
    }
}
