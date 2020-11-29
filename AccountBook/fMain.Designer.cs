
namespace AccountBook
{
    partial class fMain
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.dv1 = new System.Windows.Forms.DataGridView();
            this.dv2 = new System.Windows.Forms.DataGridView();
            this.dataGridView3 = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.dvCreditInfo = new System.Windows.Forms.DataGridView();
            this.dvAccountInfo = new System.Windows.Forms.DataGridView();
            this.dvSettingInfo = new System.Windows.Forms.DataGridView();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dv1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dv2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView3)).BeginInit();
            this.tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dvCreditInfo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dvAccountInfo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dvSettingInfo)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1094, 796);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.dataGridView3);
            this.tabPage1.Controls.Add(this.dv2);
            this.tabPage1.Controls.Add(this.dv1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1086, 770);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Home";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(192, 74);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // dv1
            // 
            this.dv1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dv1.Location = new System.Drawing.Point(37, 346);
            this.dv1.Name = "dv1";
            this.dv1.RowTemplate.Height = 23;
            this.dv1.Size = new System.Drawing.Size(240, 334);
            this.dv1.TabIndex = 0;
            // 
            // dv2
            // 
            this.dv2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dv2.Location = new System.Drawing.Point(428, 346);
            this.dv2.Name = "dv2";
            this.dv2.RowTemplate.Height = 23;
            this.dv2.Size = new System.Drawing.Size(240, 334);
            this.dv2.TabIndex = 1;
            // 
            // dataGridView3
            // 
            this.dataGridView3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView3.Location = new System.Drawing.Point(789, 335);
            this.dataGridView3.Name = "dataGridView3";
            this.dataGridView3.RowTemplate.Height = 23;
            this.dataGridView3.Size = new System.Drawing.Size(240, 334);
            this.dataGridView3.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(64, 72);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 12);
            this.label1.TabIndex = 3;
            this.label1.Text = "IMPORT";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(165, 72);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(11, 12);
            this.label2.TabIndex = 4;
            this.label2.Text = "0";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(165, 105);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(11, 12);
            this.label3.TabIndex = 6;
            this.label3.Text = "0";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(64, 105);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(54, 12);
            this.label4.TabIndex = 5;
            this.label4.Text = "EXPORT";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(165, 135);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(11, 12);
            this.label5.TabIndex = 8;
            this.label5.Text = "0";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(64, 135);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(45, 12);
            this.label6.TabIndex = 7;
            this.label6.Text = "TOTAL";
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.dvCreditInfo);
            this.tabPage3.Controls.Add(this.dvAccountInfo);
            this.tabPage3.Controls.Add(this.dvSettingInfo);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(1086, 770);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Setting";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // dvCreditInfo
            // 
            this.dvCreditInfo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dvCreditInfo.Location = new System.Drawing.Point(805, 288);
            this.dvCreditInfo.Name = "dvCreditInfo";
            this.dvCreditInfo.RowTemplate.Height = 23;
            this.dvCreditInfo.Size = new System.Drawing.Size(240, 334);
            this.dvCreditInfo.TabIndex = 5;
            // 
            // dvAccountInfo
            // 
            this.dvAccountInfo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dvAccountInfo.Location = new System.Drawing.Point(444, 299);
            this.dvAccountInfo.Name = "dvAccountInfo";
            this.dvAccountInfo.RowTemplate.Height = 23;
            this.dvAccountInfo.Size = new System.Drawing.Size(240, 334);
            this.dvAccountInfo.TabIndex = 4;
            // 
            // dvSettingInfo
            // 
            this.dvSettingInfo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dvSettingInfo.Location = new System.Drawing.Point(53, 299);
            this.dvSettingInfo.Name = "dvSettingInfo";
            this.dvSettingInfo.RowTemplate.Height = 23;
            this.dvSettingInfo.Size = new System.Drawing.Size(240, 334);
            this.dvSettingInfo.TabIndex = 3;
            // 
            // fMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1094, 796);
            this.Controls.Add(this.tabControl1);
            this.Name = "fMain";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.fMain_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dv1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dv2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView3)).EndInit();
            this.tabPage3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dvCreditInfo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dvAccountInfo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dvSettingInfo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView dataGridView3;
        private System.Windows.Forms.DataGridView dv2;
        private System.Windows.Forms.DataGridView dv1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.DataGridView dvCreditInfo;
        private System.Windows.Forms.DataGridView dvAccountInfo;
        private System.Windows.Forms.DataGridView dvSettingInfo;
    }
}

