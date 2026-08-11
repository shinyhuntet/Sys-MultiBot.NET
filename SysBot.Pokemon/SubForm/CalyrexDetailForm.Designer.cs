using System.ComponentModel.Design;

namespace SysBot.Pokemon;

partial class CalyrexDetailForm
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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalyrexDetailForm));
        CalyrexText = new System.Windows.Forms.TextBox();
        CalyrexPic = new System.Windows.Forms.PictureBox();
        CalyrexGroup = new System.Windows.Forms.GroupBox();
        HorseGroup = new System.Windows.Forms.GroupBox();
        HorsePic = new System.Windows.Forms.PictureBox();
        HorseText = new System.Windows.Forms.TextBox();
        ((System.ComponentModel.ISupportInitialize)CalyrexPic).BeginInit();
        CalyrexGroup.SuspendLayout();
        HorseGroup.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)HorsePic).BeginInit();
        SuspendLayout();
        // 
        // CalyrexText
        // 
        CalyrexText.BackColor = System.Drawing.SystemColors.Control;
        CalyrexText.Font = new System.Drawing.Font("Yu Gothic UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 128);
        CalyrexText.Location = new System.Drawing.Point(29, 158);
        CalyrexText.Multiline = true;
        CalyrexText.Name = "CalyrexText";
        CalyrexText.ReadOnly = true;
        CalyrexText.Size = new System.Drawing.Size(240, 240);
        CalyrexText.TabIndex = 0;
        CalyrexText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
        // 
        // CalyrexPic
        // 
        CalyrexPic.Location = new System.Drawing.Point(29, 22);
        CalyrexPic.Name = "CalyrexPic";
        CalyrexPic.Size = new System.Drawing.Size(130, 130);
        CalyrexPic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        CalyrexPic.TabIndex = 1;
        CalyrexPic.TabStop = false;
        // 
        // CalyrexGroup
        // 
        CalyrexGroup.Controls.Add(CalyrexPic);
        CalyrexGroup.Controls.Add(CalyrexText);
        CalyrexGroup.Location = new System.Drawing.Point(12, 12);
        CalyrexGroup.Name = "CalyrexGroup";
        CalyrexGroup.Size = new System.Drawing.Size(300, 430);
        CalyrexGroup.TabIndex = 2;
        CalyrexGroup.TabStop = false;
        CalyrexGroup.Text = "Calyrex Detail";
        // 
        // HorseGroup
        // 
        HorseGroup.Controls.Add(HorsePic);
        HorseGroup.Controls.Add(HorseText);
        HorseGroup.Location = new System.Drawing.Point(327, 12);
        HorseGroup.Name = "HorseGroup";
        HorseGroup.Size = new System.Drawing.Size(300, 430);
        HorseGroup.TabIndex = 3;
        HorseGroup.TabStop = false;
        HorseGroup.Text = "Horse Detail";
        // 
        // HorsePic
        // 
        HorsePic.Location = new System.Drawing.Point(29, 22);
        HorsePic.Name = "HorsePic";
        HorsePic.Size = new System.Drawing.Size(130, 130);
        HorsePic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        HorsePic.TabIndex = 1;
        HorsePic.TabStop = false;
        // 
        // HorseText
        // 
        HorseText.BackColor = System.Drawing.SystemColors.Control;
        HorseText.Font = new System.Drawing.Font("Yu Gothic UI", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 128);
        HorseText.Location = new System.Drawing.Point(29, 158);
        HorseText.Multiline = true;
        HorseText.Name = "HorseText";
        HorseText.ReadOnly = true;
        HorseText.Size = new System.Drawing.Size(240, 240);
        HorseText.TabIndex = 0;
        HorseText.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
        // 
        // CalyrexDetailForm
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        BackColor = System.Drawing.SystemColors.HighlightText;
        ClientSize = new System.Drawing.Size(639, 461);
        Controls.Add(HorseGroup);
        Controls.Add(CalyrexGroup);
        Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
        Name = "CalyrexDetailForm";
        Controls.SetChildIndex(CalyrexGroup, 0);
        Controls.SetChildIndex(HorseGroup, 0);
        ((System.ComponentModel.ISupportInitialize)CalyrexPic).EndInit();
        CalyrexGroup.ResumeLayout(false);
        CalyrexGroup.PerformLayout();
        HorseGroup.ResumeLayout(false);
        HorseGroup.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)HorsePic).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
    #endregion

    private System.Windows.Forms.TextBox CalyrexText;
    private System.Windows.Forms.PictureBox CalyrexPic;
    private System.Windows.Forms.GroupBox CalyrexGroup;
    private System.Windows.Forms.GroupBox HorseGroup;
    private System.Windows.Forms.PictureBox HorsePic;
    private System.Windows.Forms.TextBox HorseText;
}
