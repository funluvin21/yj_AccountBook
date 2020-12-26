using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text;

namespace AccountBook.Class
{
    public class RTFDataAgent : Component
    {
        //////////////////////////////////////////////////////////////////
        // PRIVATE CONSTANTS AND VARIABLES
        //////////////////////////////////////////////////////////////////

        //private RTFDataControl DataControl = new RTFDataControl();
        public delegate void rDataEventHandler(object sender, rDataEventArgs e);
        public event rDataEventHandler rDataEvent;

        public delegate void PushEventHandler(object sender, PushEventArgs e);
        public event PushEventHandler PushEvent;

        public bool bAutoNextRequest = true;
        public uint LastSeqID = 0;

        public bool bManualFinish = false;

        public RTFDataAgent()
        {
            //DataControl.DataEvent += new RTFDataControl.DataEventHandler(this.DataRecvEvent);
            //  Application.AddMessageFilter(this);

            //개체 삭제시 RTFGlobal 개체에 통보
            this.Disposed += new EventHandler(RtfGlobal.SessionManager.SessionManager_DataAgentDisposed);
        }

        protected override void Dispose(bool disposing)
        {
            //대기큐 클리어

            //리얼요청 해제
            rDataEvent = null;
            PushEvent = null;

            base.Dispose(disposing);
        }

        public void RequestNextData()
        {
            if (RtfGlobal.SessionManager != null)
            {
                RtfGlobal.SessionManager.RequestNextDataManual(this);
            }
        }

        //20130725 [3]세션매니저에게 수동으로 넥스트조회를 요청
        public void RequestNextDataEx(int SvrNo, byte[] ByteSndData)
        {
            if (RtfGlobal.SessionManager != null)
            {
                RtfGlobal.SessionManager.RequestNextDataManualEx(SvrNo, this, ByteSndData);
            }
        }

        public void CancelTranData()
        {
            if (RtfGlobal.SessionManager != null)
            {
                RtfGlobal.SessionManager.Data_Cancel_ByDataAgent(this);
                RtfGlobal.SessionManager.RequestExitDataManual(this);
            }
        }

        public void Data_Request(string TrCode, int SvrNo, int DataSeq, int CompressValue, String SendData)
        {//string 전송..
            //선행화
            //DataControl.Data_Request(TrCode, SvrNo, DataSeq, CompressValue, SendData);

            //실전.. DataControl 사용안함.
            //RtfGlobal.ICMManager에 RTFDataAgent의 HashCode를 키값으로 전송
            if (RtfGlobal.SessionManager != null)
            {
                //IntPtr hIcmMng = RtfGlobal.ICMManager.Handle;
                //if ( IsDisposed == false )
                //{
                byte[] buffData = Encoding.Default.GetBytes(SendData);
                LastSeqID = RtfGlobal.SessionManager.Data_Request(1, TrCode, SvrNo, DataSeq, CompressValue, SendData, buffData, this);
                //}
            }
        }

        public void Data_Request(string TrCode, int SvrNo, int DataSeq, int CompressValue, byte[] bSendData)
        {//byte 전송..
            if (RtfGlobal.SessionManager != null)
            {
                string SendData = string.Empty;
                LastSeqID = RtfGlobal.SessionManager.Data_Request(2, TrCode, SvrNo, DataSeq, CompressValue, SendData, bSendData, this);
            }
        }

        public uint Data_RequestEx(string TrCode, String SendData)
        {
            return Data_RequestEx(TrCode, 0, 1, 0, SendData);
        }

        public uint Data_RequestEx(string TrCode, int SvrNo, int DataSeq, int CompressValue, String SendData)
        {
            if (RtfGlobal.SessionManager != null)
            {
                byte[] buffData = Encoding.Default.GetBytes(SendData);
                LastSeqID = RtfGlobal.SessionManager.Data_Request(1, TrCode, SvrNo, DataSeq, CompressValue, SendData, buffData, this);
            }
            else
            {
                LastSeqID = 0;
            }
            return LastSeqID;
        }

        public void Real_Reg(string TrCode, String SendData)
        {
            //선행화
            //DataControl.Real_Reg(TrCode, SendData);

            //실전
            //RtfGlobal.SessionManager.RequestRTData(TrCode, SendData, this);
        }

        public uint RequestRTData(ushort wTrCode, string strCodes)
        {
            return RtfGlobal.SessionManager.RequestRTData(wTrCode, strCodes, this);
        }

        public void CloseRTData(uint nReqID)
        {
            RtfGlobal.SessionManager.CloseRTData(nReqID, this);
        }

        delegate void ThreadSafe_DataRecvEvent_CallBack(object sender, DataEventArgs e);
        public void ThreadSafe_DataRecvEvent(object sender, DataEventArgs e)
        {
            try
            {
                //if (RtfGlobal.MainForm.InvokeRequired)
                //{
                //    ThreadSafe_DataRecvEvent_CallBack d = new ThreadSafe_DataRecvEvent_CallBack(ThreadSafe_DataRecvEvent);
                //    RtfGlobal.MainForm.Invoke(d, new object[] { sender, e });
                // }
                //else
                //{
                DataRecvEvent(sender, e);
                //}
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {

            }
        }

        delegate void ThreadSafe_PushEvent_CallBack(object sender, PushEventArgs e);
        public void ThreadSafe_PushEvent(object sender, PushEventArgs e)
        {
            try
            {
                //if (RtfGlobal.MainForm.InvokeRequired)
                // {
                //    ThreadSafe_PushEvent_CallBack d = new ThreadSafe_PushEvent_CallBack(ThreadSafe_PushEvent);
                //    RtfGlobal.MainForm.Invoke(d, new object[] { sender, e });
                //}
                //else
                //{
                if (PushEvent != null)
                {
                    PushEvent(this, e);
                }
                //}
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {

            }
        }

        public void DataRecvEvent(object sender, DataEventArgs e)
        {

            string aData = e.Rec_Data;
            int KeySeq = 0;
            int SKey = 0;
            int TrCode = 0;
            int SvrNo = 0;
            int DataSeq = 0;
            int msgcd = 0;
            int FromHandle = 0;

            if (e.Pnt != -1)
            {
                while (SKey > -1)
                {
                    SKey = aData.IndexOf('|');
                    switch (KeySeq)
                    {
                        case 0:
                            TrCode = Convert.ToInt32(aData.Substring(0, SKey));
                            break;
                        case 1:
                            SvrNo = Convert.ToInt32(aData.Substring(0, SKey));
                            break;
                        case 2:
                            DataSeq = Convert.ToInt32(aData.Substring(0, SKey));
                            break;
                        case 3:
                            msgcd = Convert.ToInt32(aData.Substring(0, SKey));
                            break;
                        case 4:
                            FromHandle = Convert.ToInt32(aData.Substring(0, SKey));
                            break;
                    }
                    KeySeq++;
                    aData = aData.Substring(SKey + 1, aData.Length - SKey - 1);
                }
            }

            rDataEventArgs args = new rDataEventArgs(e.TrCode, e.Pnt, SvrNo, DataSeq, aData, e.Rcv_Info);
            if (rDataEvent != null)
            {
                rDataEvent(this, args);
            }

            //20121015 전송용 데이터 클리어
            e.Rcv_Info.R_Data = null;


            //////////////////////////////////////////////////////////////////////////
            //20130614 메모리 해제
            e.Rcv_Info.R_RawData = null;
            //////////////////////////////////////////////////////////////////////////

            //최종작업
            //Marshal.FreeHGlobal((IntPtr)e.Pnt);
            //GC.Collect();
        }

        public object GetTargetInfo()
        {
            if (rDataEvent != null)
            {
                return rDataEvent.Target;
            }
            return null;
        }
    }

    public class PushEventArgs : EventArgs
    {
        public PushEventArgs(int TrCode, string strCode, string[] arrData)
        {
            this.TrCode = TrCode;
            this.strCode = strCode;
            this.arrData = arrData;
        }
        public int TrCode;
        public string strCode;
        public string[] arrData;
    }

    public class DataEventArgs : EventArgs
    {
        public int TrCode;
        public int Pnt;
        public string Rec_Data;
        public CommRcvInfo Rcv_Info;
        public DataEventArgs(int rTrCode, int rPnt, string rRec_Data, CommRcvInfo rRcv_Info)
        {
            this.TrCode = rTrCode;
            this.Pnt = rPnt;
            this.Rec_Data = rRec_Data;
            this.Rcv_Info = rRcv_Info;
        }
    }

    public struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        [MarshalAs(UnmanagedType.LPStr)]
        public string lpData;
    }

    public class rDataEventArgs : EventArgs
    {
        public int TrCode;          //TRCODE

        public int Pnt;             //사용안함
        public int SvrNo;           //사용안함
        public int DataSeq;         //사용안함
        public int CompressValue;   //사용안함
        public string Rec_Data;     //사용안함

        public CommRcvInfo Rcv_Info;//수신데이터 정보

        public rDataEventArgs(int rTrCode, int rPnt, int rSvrNo, int rDataSeq, string Data, CommRcvInfo rRcv_Info)
        {
            this.TrCode = rTrCode;
            this.Pnt = rPnt;
            this.SvrNo = rSvrNo;
            this.DataSeq = rDataSeq;
            this.Rec_Data = Data;
            this.Rcv_Info = rRcv_Info;
        }
    }

    public struct Data_Rcv
    {
        public ArrayList R_DataList;    //데이터 Array
        public ArrayList R_LoopDataList;
        public ArrayList R_ColorList;  //컬러 Array
    }

    public struct Prsing_Struct
    {
        public int TrCode;           //TR 명
        public int Start_Byte;      //시작위치
        public int Data_Type;        //테이터형
        public int Data_Len;         //테이터길이
        public int Float_Len;        //소숫점자리수
        public int DisPlay_Effect1;  // 표시효과1
        public int DisPlay_Effect2;  // 표시효과2
    }

    //DATA,    0,     C,     6,   0,AT_NORMAL,AT_NORMAL,// 단축코드 

}