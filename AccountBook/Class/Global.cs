using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccountBook.Class
{
    public delegate void AttributeEventHandler(object sender, Attribute attr);

    public class RtfGlobal
    {

        //***********************************************
        //Singleton pattern [ 1개의 인스턴스만 생성 ]
        //***********************************************
        private static RtfGlobal _rtfglobal = null;
        public static RtfGlobal Instance { get { if (_rtfglobal == null) { _rtfglobal = new RtfGlobal(); } return _rtfglobal; } }

        //매니저
        private RtfSessionManager _sessionmanager = null;   //통신 관리자 인스턴스

        //***********************************************
        //외부 참조용 Property [ 읽기 전용 ]
        //***********************************************
        public static RtfSessionManager SessionManager { get { return Instance._sessionmanager; } }     // RtfGlobal.SessionManager 과 같이 사용가능

        private RtfGlobal()
        {
            //한번만 생성된다.
            Debug.WriteLine("< RtfGlobal 객체 생성 >", "RtfGlobal");

            //개별 관리자 생성
            _sessionmanager = new RtfSessionManager();

        }

        public static string LoginID
        {
            get { return "master"; }
            //set { MasterManager.LoginID = value; }
        }
        /*
        public static string LoginPASS
        {
            get { return MasterManager.LoginPASS; }
            set { MasterManager.LoginPASS = value; }
        }

        public static string IF_NM                           //성명
        {
            get { return MasterManager.IF_NM; }
            set { MasterManager.IF_NM = value; }
        }
        public static string IF_DEP_CD                       //부서코드
        {
            get { return MasterManager.IF_DEP_CD; }
            set { MasterManager.IF_DEP_CD = value; }
        }
        public static string IF_TEAM_MBR_TP_CD               //팀구성원구분코드
        {
            get { return MasterManager.IF_TEAM_MBR_TP_CD; }
            set { MasterManager.IF_TEAM_MBR_TP_CD = value; }
        }
        public static string IF_ACCSS_AUTHID                 //접근권한 ID
        {
            get { return MasterManager.IF_ACCSS_AUTHID; }
            set { MasterManager.IF_ACCSS_AUTHID = value; }
        }
        public static string IF_EMP_NO                       //사원번호
        {
            get { return MasterManager.IF_EMP_NO; }
            set { MasterManager.IF_EMP_NO = value; }
        }
        public static string IF_REG_GRND_DOC_ID              //등록근거문서 ID
        {
            get { return MasterManager.IF_REG_GRND_DOC_ID; }
            set { MasterManager.IF_REG_GRND_DOC_ID = value; }
        }
        public static string IF_RELEAS_DD                    //해제일자
        {
            get { return MasterManager.IF_RELEAS_DD; }
            set { MasterManager.IF_RELEAS_DD = value; }
        }
        public static string IF_REG_DD                       //등록일자
        {
            get { return MasterManager.IF_REG_DD; }
            set { MasterManager.IF_REG_DD = value; }
        }
        public static string IF_CREAT_DDTM                   //생성일시
        {
            get { return MasterManager.IF_CREAT_DDTM; }
            set { MasterManager.IF_CREAT_DDTM = value; }
        }
        public static string IF_CREATR_ID                    //생성자 ID
        {
            get { return MasterManager.IF_CREATR_ID; }
            set { MasterManager.IF_CREATR_ID = value; }
        }
        public static string IF_ADJ_DDTM                     //수정일시
        {
            get { return MasterManager.IF_ADJ_DDTM; }
            set { MasterManager.IF_ADJ_DDTM = value; }
        }
        public static string IF_ADJPRN_ID                    //수정자 ID
        {
            get { return MasterManager.IF_ADJPRN_ID; }
            set { MasterManager.IF_ADJPRN_ID = value; }
        }
        public static string IF_PW_CHG_END_DD                //비밀번호 변경 만료일자
        {
            get { return MasterManager.IF_PW_CHG_END_DD; }
            set { MasterManager.IF_PW_CHG_END_DD = value; }
        }
        public static string IF_CLIENT_RECONN_TRY_CNT        //클라이언트 재접속 시도수
        {
            get { return MasterManager.IF_CLIENT_RECONN_TRY_CNT; }
            set { MasterManager.IF_CLIENT_RECONN_TRY_CNT = value; }
        }
        public static string IF_MAC_ADDR_USE_YN              //MAC 주소 사용여부
        {
            get { return MasterManager.IF_MAC_ADDR_USE_YN; }
            set { MasterManager.IF_MAC_ADDR_USE_YN = value; }
        }
        public static string IF_MAC_ADDR                     //MAC 주소
        {
            get { return MasterManager.IF_MAC_ADDR; }
            set { MasterManager.IF_MAC_ADDR = value; }
        }
        public static string IF_SHR_WRTRPT_AUTO_PROCS_YN     //지분보고서 자동처리여부
        {
            get { return MasterManager.IF_SHR_WRTRPT_AUTO_PROCS_YN; }
            set { MasterManager.IF_SHR_WRTRPT_AUTO_PROCS_YN = value; }
        }
        public static string IF_SCREN_CAP_USE_YN             //화면캡쳐사용여부
        {
            get { return MasterManager.IF_SCREN_CAP_USE_YN; }
            set { MasterManager.IF_SCREN_CAP_USE_YN = value; }
        }
        public static string IF_TF_TP_CD                                //T/F구분코드
        {
            get { return MasterManager.IF_TF_TP_CD; }
            set { MasterManager.IF_TF_TP_CD = value; }
        }
        */

    }
}