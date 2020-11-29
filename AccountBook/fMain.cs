using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AccountBook.Class;

namespace AccountBook
{
    public partial class fMain : Form
    {
        DBMysql _db = new DBMysql();
        int cnt = 0;

        public fMain()
        {
            InitializeComponent();
        }

        private void fMain_Load(object sender, EventArgs e)
        {
            _db.Connection();
            InitSetting();
        }

        public void InitSetting()
        {
            Config.settingds = _db.SelectDetail(Config.Tables[(int)Config.eTName._setting], "import_info as '수입계정', export_info as '지출계정'");
            dvSettingInfo.DataSource = Config.settingds.Tables[0];
            cnt = 0;
            Config.ImportInfo = new string[Config.settingds.Tables[0].Rows.Count];
            Config.ExportInfo = new string[Config.settingds.Tables[0].Rows.Count];
            foreach (DataRow r in Config.settingds.Tables[0].Rows)
            {
                if (r["수입계정"] != DBNull.Value)
                {
                    Config.ImportInfo[cnt] = r["수입계정"].ToString();
                }
                if (r["지출계정"] != DBNull.Value)
                {
                    Config.ExportInfo[cnt] = r["지출계정"].ToString();
                }
                cnt++;
            }
            Config.ImportInfo = Config.ImportInfo.Where(c => c != null).ToArray();
            Config.ExportInfo = Config.ExportInfo.Where(c => c != null).ToArray();

            cnt = 0;
            string AccStr = "bank_name as '은행명', account_name as '계좌명', connect_card as '연결체크카드', first_balance as '최초잔액', memo as '메모'";
            Config.accountds = _db.SelectDetail(Config.Tables[(int)Config.eTName._account], AccStr);
            dvAccountInfo.DataSource = Config.accountds.Tables[0];
            dvAccountInfo.Columns[3].DefaultCellStyle.Format = "###,##0";
            dvAccountInfo.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            Config.AccountName = new string[Config.accountds.Tables[0].Rows.Count];
            Config.ConnectCard = new string[Config.accountds.Tables[0].Rows.Count];
            foreach (DataRow r in Config.accountds.Tables[0].Rows)
            {
                Config.AccountName[cnt] = r["계좌명"].ToString();
                Config.ConnectCard[cnt] = r["연결체크카드"].ToString();
                cnt++;
            }

            cnt = 0;
            string CreditStr = "credit_bank as '신용카드사', card_name as '카드명', withdraw_account as '출금계좌', bank_name as '은행명', withdraw_date as '출금일', use_term as '사용실적기간'";
            Config.creditds = _db.SelectDetail(Config.Tables[(int)Config.eTName._credit], CreditStr);
            dvCreditInfo.DataSource = Config.creditds.Tables[0];
            Config.CardName = new string[Config.creditds.Tables[0].Rows.Count];
            foreach (DataRow r in Config.creditds.Tables[0].Rows)
            {
                Config.CardName[cnt] = r["카드명"].ToString();
                cnt++;
            }
        }
    }
}
