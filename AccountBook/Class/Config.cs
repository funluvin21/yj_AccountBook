using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace AccountBook.Class
{
    public static class Config
    {
        public static string Database = "account_db";
        //public static string Server = "192.168.1.20";
        //public static string Port = "3306";
        public static string Server = "yjhome.duckdns.org";
        public static string Port = "7336";
        public static string Server = "yjhome.duckdns.org";
        public static string Port = "7336";
        public static string UserID = "funluvin";
        public static string UserPassword = "Tmdn900*18";

        public static string[] Tables = { "setting_info", "account_info", "credit_info", "account_book" };
        public enum eTName : int { _setting, _account, _credit, _book }

        public static string[] UseType = { "수입", "지출", "계좌이동" };
        public static string[] AccountI = { "현금", "계좌입금" };
        public static string[] AccountE = { "현금", "체크카드", "신용카드", "타인계좌이체" };
        public static string[] Cash = { "내지갑" };
        public static string[] BankName = { "국민은행","우리은행","신한은행","기업은행","KEB하나은행","한국씨티은행","SC(제일)은행","산업은행","상호저축은행","새마을금고은행",
                                            "우체국은행","농협은행","수협은행","신협은행","산림조합은행","경남은행","광주은행","대구은행","부산은행","제주은행",
                                            "전북은행","카카오뱅크은행","K뱅크은행","TOSS은행" };
        public static string[] CreditBankName = { "KB국민","우리","신한","현대","삼성","롯데","하나","NH농협","씨티","SC",
                                                  "수협","MG","우체국","K뱅크","카카오" };
        public static string[] AccountName = null;
        public static string[] ConnectCard = null;
        public static string[] CardName = null;
        public static string[] ImportInfo = null;
        public static string[] ExportInfo = null;

        public static DataSet settingds = null;
        public static DataSet accountds = null;
        public static DataSet creditds = null;
        public static DataSet books = null;
        public static DataTable importdt = null;
        public static DataTable exportdt = null;
        public static DataTable totaldt = null;
        public static int CreditDate = 0;
        public static int[] CreditTerm = { 0, 0 };
    }
}
