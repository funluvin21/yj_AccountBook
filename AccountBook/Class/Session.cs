using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Collections;
using System.Runtime.InteropServices;
//using CommSocketMngrLib;

namespace AccountBook.Class
{
    //public partial class RtfSession : Component
    public partial class RtfSession
    {
        public const string SPChar = "|";

        //public CommSocket socket = new CommSocket();
        public string session_name = string.Empty;
        public RtfAsyncSocket m_async_socket;

        //public bool bConnected = false;//세션 접속 여부
        //세션 접속 여부
        public bool bConnected
        {
            get
            {
                return (m_async_socket == null) ? false : m_async_socket.Connected;
            }
        }

        public TRItem m_tritem = null; //현재 할당된 TRItem

        public RtfSessionState state = RtfSessionState.ready_attach;

        CommRcvInfo last_rcv_info;  //

        private RtfSessionType _type;
        public RtfSessionType type
        {
            get { return _type; }
        }

        private static uint sid = 0;
        private uint _id;
        public uint id
        {
            get { return _id; }
        }

        public delegate void ReadyEventHandler(object sender, ReadyEventArgs e);
        public event ReadyEventHandler ReadyEvent;

        public bool IsPushSession()
        {
            return type == RtfSessionType.push ? true : false;
        }

        public RtfSession(RtfSessionManager icmmanager, RtfSessionType _type, string session_name)
        {
            Debug.WriteLine(string.Format("RtfSession 생성 : 세션명={0}, 타입={1}", session_name, _type));

            this.session_name = session_name;

            RtfSockType socktype = (_type == RtfSessionType.push) ? RtfSockType.push : RtfSockType.tran;
            m_async_socket = new RtfAsyncSocket(this, socktype, session_name + "의 소켓");
            m_async_socket.OnConnect += new SessionEventHandler(AsyncSocket_OnConnect);
            m_async_socket.OnClose += new SessionEventHandler(AsyncSocket_OnClose);
            m_async_socket.OnSend += new SessionEventHandler(AsyncSocket_OnSend);
            m_async_socket.OnReceive += new SessionEventHandler(AsyncSocket_OnReceive);

            //대기 상태로 전환시에 ICMManager에 통보
            this._id = sid++;
            this.ReadyEvent += new ReadyEventHandler(icmmanager.OnSessionReady);
            this._type = _type;

            //socket.OnData += new _ICommSocketEvents_OnDataEventHandler(Comcon_OnData);        //VC++ Socket Com
            //m_async_socket.OnData += new RtfAsyncSocket.RtfAsyncSocket_OnDataEventHandler(Comcon_OnData);//C# Socket
            //Data_Reciver.R_DataList = new ArrayList();
            //Data_Reciver.R_ColorList = new ArrayList();
            //Data_Reciver.R_LoopDataList = new ArrayList();
        }


        public void ConnectToServer(string strIP, string strPort)
        {
            //int nSockType = IsPushSession() ? 1 : 0;
            //this.m_async_socket.IPAddr_Input(strIP, strPort, nSockType);
            //this.m_async_socket.Open();
            //접속
            this.m_async_socket.Connect(strIP, strPort);
        }

        /*
        public void ReconnectToServer()
        {
            if (this.m_async_socket.Connected == false)
            {
                this.m_async_socket.Reconnect();
            }
        }
        */

        public void ShutDownSession()
        {
            this.m_async_socket.Close();
            this.DettachTRItem();
        }

        public object objLockReconnect = new object();

        //20130130 화면종료시 재접속 ( 현재 접속중인 세션을 강제 접속해제 )
        public void ReconnectToServerByScreenClose()
        {
            lock (objLockReconnect)
            {
                if (this.m_async_socket.Connected == true)
                {
                    if (_bReConnectByDequeueProc == false)
                    {
                        _bReConnectByDequeueProc = true;

                        //////////////////////////////////////////////////////////////////////////
                        // 20130115 강제종료TR 전송
                        /*
                        if (_tritem != null)
                        {
                            //this.state = RtfSessionState.ready_exit;
                            socket.Data_Send(Convert.ToInt32(_tritem.strTrCode),    //
                                        _tritem.nSvrNo,            // Dest_way
                                        (int)_tritem.nSeq,         // TRItem 시퀀스
                                        _tritem.nDataSeq,          // 유저데이터
                                        _tritem.nDataType,         // 0:일반 1:대용량
                                        0,                         // 넥스트사이즈
                                        "00000000",                // 유저아이디
                                        "",                        // 전송데이터 
                                        "1");                      // 강제종료플래그
                        }
                        */
                        //////////////////////////////////////////////////////////////////////////

                        this.DettachTRItem();
                        this.m_async_socket.Reconnect();
                    }
                }
            }
        }

        //20130130 TR할당시 종료된 세션일 경우 재접속
        public bool _bReConnectByDequeueProc = false;
        //public int _nReConnectFailedCount = 0;
        public void ReconnectToServerBySessionDisconnected()
        {
            lock (objLockReconnect)
            {
                if (_bReConnectByDequeueProc == false)
                {
                    _bReConnectByDequeueProc = true;
                    this.DettachTRItem();
                    this.m_async_socket.Reconnect();
                }
            }
        }

        void AsyncSocket_OnConnect(object sender, SessionEventArgs e)
        {
            //20130130 재접속중 플래그를 해제한다.
            lock (objLockReconnect)
            {
                _bReConnectByDequeueProc = false;
            }

            if (e.Connected)
            {
                m_last_receive_time = DateTime.Now.Ticks;

                Debug.WriteLine(session_name + " [ Socket Connected! ]");

                //20130107 재접속중 플래그를 해제한다. 카운트 초기화.
                //_bReConnectByDequeueProc = false;
                //_nReConnectFailedCount = 0;

                //로그인 세션인 경우 접속되었음을 알린다.
                if (IsPushSession())
                {
                    //세션매니저에 푸쉬접속 통보
                    RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.push_connected);
                }
                else
                {
                    //세션매니저에 TR접속 통보
                    RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.connected);
                }
            }
            else
            {
                //20130107 5번까지는 재접속이 수행되도록 한다.
                //_nReConnectFailedCount++;
                //if (_nReConnectFailedCount < 5)
                //{
                //    _bReConnectByDequeueProc = false;
                //}

                Debug.WriteLine(session_name + " [ Socket Connect Failure! ]");
                //세센매니저에 접속실패 통보
                RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.connect_failure);
            }
        }

        void AsyncSocket_OnClose(object sender, SessionEventArgs e)
        {
            //20121005 접속종료시 처리
            Debug.WriteLine(session_name + " [ Socket Disconnected! ]");

            //처리중인 tr을 버리고 대기상태로 만든다.
            if (IsPushSession() == false)
            {
                //20130130 처리중인 TR이 있는데 소켓이 끊어진 경우...
                /*
                TRItem cur_tritem = GetAttachedTRItem();
                if (cur_tritem != null)
                {
                    //처리실패한 TR리스트에 추가해서 관리
                    //재접속 성공후 다시 서버에 요청하는 방식등으로 관리... 구현은 안함.
                }
                */

                //할당된 TR을 해제한다.
                SetSessionReadyState(true);

                //20130130 접속종료시 다시 접속을 시도한다.
                //Debug.WriteLine(session_name + " [ TR Session Disconnected! ]");
                //셧다운 상태가 아니라면
                // 2013/10/22 세션 자동접속을 수동접속으로 변경
                /*
                if (RtfGlobal.SessionManager.IsShutDown == false)
                {
                    ReconnectToServerBySessionDisconnected();
                }
                 */
            }

            //세션매니저에 접속종료 통보
            RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.disconnected);
        }

        void AsyncSocket_OnSend(object sender, SessionEventArgs e)
        {
            //20121018 : 서버전송 시간 기록
            if (IsPushSession() == false)
            {
                TRItem cur_tritem = GetAttachedTRItem();
                if (cur_tritem != null)
                {
                    int LogCount = cur_tritem.AddSendTime(DateTime.Now);
                    //2개 이상이면 이전 수신부터 현재 요청까지의 딜레이를 출력
                    if (LogCount > 1)
                    {
                        Debug.WriteLine(string.Format("수신에서 넥스트 요청까지 지연시간 {0}", cur_tritem.GetLastDelayTimeSpan()));
                    }
                    Debug.WriteLine(string.Format(session_name + " [ OnSend TR코드={0}, {1} bytes! 소켓에서 서버로 데이터 전송 완료... ]", cur_tritem.strTrCode, e.Length));
                }
            }
            //Debug.WriteLine(string.Format(sock_name + " [ OnSend TR코드={0}, {1} bytes! 소켓에서 서버로 데이터 전송 완료... ]", nSendingTrCode, e.BytesTransferred));
        }


        void AsyncSocket_OnReceive(object sender, SessionEventArgs e)
        {
            if (IsPushSession() == false)
            {
                m_last_receive_time = DateTime.Now.Ticks;

                TRItem cur_tritem = GetAttachedTRItem();
                if (cur_tritem != null)
                {
                    CommRcvInfo rcv_info = e.GetReceiveInfo();
                    if (rcv_info != null)
                    {
                        //시퀀스가 동일한지 체크
                        //if (cur_tritem.nSeq == rcv_info.R_Handle)
                        //{
                        // 2013/10/22 세션 자동접속을 수동접속으로 변경
                        // 폴링(91101) 응답시 m_pollingTag 초기화
                        if (rcv_info.R_TrCode.ToString() == "91101")
                            RtfGlobal.SessionManager.m_pollingTag = 0;

                        cur_tritem.SetReceiveTime(e._rcv_time);
                        Debug.WriteLine(string.Format("전송에서 수신까지 ... 소요시간 {0}", cur_tritem.GetLastTimeSpan()));

                        Debug.WriteLine(string.Format(session_name + " [ 소켓에서 데이터 받음 ] TR코드={0}, 데이터={1}...", rcv_info.R_TrCode, rcv_info.R_Data.Substring(0, Math.Min(64, rcv_info.R_Data.Length))));
                        OnRcvIQData(cur_tritem, rcv_info);
                        //}
                    }
                }
            }
        }

        //20121018 TR데이터 수신처리
        public void OnRcvIQData(TRItem cur_tritem, CommRcvInfo rcv_info)
        {
            if (cur_tritem == null)
            {
                return;
            }

            //수신 정보를 저장함
            this.last_rcv_info = rcv_info;

            //수신데이터 헤더부분 파싱
            uint nSvrRcvSeq = (uint)rcv_info.R_Handle;

            uint nSeq = cur_tritem.nSeq;
            bool bLargeData = (type == RtfSessionType.large) ? true : false;
            bool bHasNext = (rcv_info.R_NextSize > 0) ? true : false;
            bool bFinished = true;
            bool bDataParsing = false;

            //this.InvokeRequired
            //세션에서 데이터를 전송하고 세션이 데이터를 받는다고 가정
            //서버에서 받는 값은 nSvrRcvSeq

            //서버에서 받은 시퀀스와 현재 세션의 시퀀스가 다르면 오류
            //if ( nSeq != nSvrRcvSeq )
            //{
            //    bFinished = true;
            //}

            //DataAgent의 유효성 체크
            //bool bValidDataAgent = RtfGlobal.ICMManager.IsValidDataAgent(tritem.nDataAgentHash);
            object dstDataAgent;
            bool bValidDataAgent = RtfGlobal.SessionManager.GetValidDataAgent(cur_tritem.nDataAgentHash, out dstDataAgent);
            bool bAutoNextRequest = false;
            RTFDataAgent rtfDataAgent = dstDataAgent as RTFDataAgent;
            if (rtfDataAgent != null)
            {
                bAutoNextRequest = rtfDataAgent.bAutoNextRequest;
            }

            //수동으로 세션종료를 제어할경우
            bool bManualFinish = false;
            if (rtfDataAgent != null)
            {
                bManualFinish = rtfDataAgent.bManualFinish;
            }

            //도중에 취소 요청이 들어온경우...
            bool bCanceled = (cur_tritem.status == TRItemStatus.canceled) ? true : false;

            //서버에서 응답을 받은 상태
            this.state = RtfSessionState.recieved;

            //////////////////////////////////////////////////////////////////////////
            //20120907 취소완료를 받은 경우
            bool bCancelCompleted = false;
            if (rcv_info.R_KillGbn == "2")
            {
                rcv_info.R_UserFeild = -1;//마지막데이터
                bCancelCompleted = true;
                bDataParsing = true;
                bFinished = true;
            }
            //////////////////////////////////////////////////////////////////////////
            else
            {
                //DataAgent 무효하거나 취소된 경우 데이터 파싱 스킵
                if (bValidDataAgent == false || bCanceled == true)
                {
                    if (bCanceled == true)
                    {
                        string strLog = string.Format(">>>>>>>>>>>> TRItem nSeq{0} canceled!", cur_tritem.nSeq);
                        Debug.WriteLine(strLog);
                    }

                    //대용량 데이터일 경우 서버에 서비스 종료 TR을 전송한다
                    if (bLargeData)
                    {
                        //단, 서버에서 마지막 데이터를 보낸것이면 종료 TR을 전송하지 않는다.
                        //즉 넥스트 데이터가 있는 경우 중지 TR을 전송
                        if (bHasNext == true)
                        {
                            //서버에 서비스 종료 TR을 전송한다
                            SendToServerServiceExitTR(rcv_info);
                            //종료된 것이 아님.
                            bFinished = false;
                        }
                    }
                    bDataParsing = false;
                }
                //DataAgent가 유효하고 tritem이 취소 상태가 아닐경우
                else
                {
                    //대용량 데이터일 경우 넥스트 데이터가 있다면 넥스트 조회
                    if (bLargeData)
                    {
                        if (bHasNext)
                        {
                            //20120907 자동넥스트 요청 변경
                            //대용량데이터 자동 넥스트 요청
                            //SendToServerNextData();
                            if (bAutoNextRequest)
                            {
                                //20121212 20121018 속도 테스트 < 스킵처리 >
                                SendToServerNextData(rcv_info);//cur_tritem.inType,
                                //socket.Data_Send(rcv.R_TrCode, rcv.R_DestWay, rcv.R_Handle, rcv.R_UserFeild + 1, rcv.R_Datatype, 0, rcv.R_UserID, rcv.R_NextStr, "");
                            }

                            //종료된 것이 아님.
                            bFinished = false;
                        }
                    }
                    //데이터를 파싱한다.
                    bDataParsing = true;

                    //20130725 [1] 수동종료일 경우 종료플래그를 FALSE로 셋팅... 세션이 대기상태로 전환되지 않는다.
                    if (bManualFinish)//51108 파일 관리 화면에서만 현재 사용함..
                    {
                        switch (rcv_info.R_Client_Rtn1)
                        {
                            case 10004: //파일 전송TR에서 파일의 마지막 인 경우 소켓 대기 상태로 변환..
                                bFinished = true;
                                break;
                            default://파일 전송TR에서 파일의 마지막 아닌 경우 소켓 대기 상태로 동일한 소켓으로 전송..
                                bFinished = false;
                                break;
                        }
                    }
                }
            }

            //데이터파싱할 경우 and 대상 DataAgent가 유효할경우
            if (bDataParsing == true)
            {
                int r_user_field = rcv_info.R_UserFeild;

                //취소완료 수신
                if (bCancelCompleted)
                {
                    //Data_Reciver.R_LoopDataList.Clear();
                }
                //정상데이터 수신
                else
                {
                    //첫번째 데이터이면 수신리스트 삭제
                    if (rcv_info.R_UserFeild == 1)
                    {
                        //Data_Reciver.R_LoopDataList.Clear();
                    }
                    //넥스트가 없으면 마지막데이터
                    if (rcv_info.R_NextSize == 0)
                    {
                        r_user_field = -1;
                    }
                }

                //종료시 전체 시간 출력
                if (bFinished)
                {
                    Debug.WriteLine(string.Format("### 전체 전송에서 수신까지 ... 소요시간 {0}", cur_tritem.GetTotalTimeSpan()));
                }

                //해당 화면으로 데이터 전송
                if (rtfDataAgent != null)
                {
                    int pnt = 0;
                    string SendData = rcv_info.R_TrCode.ToString() + SPChar +
                                      rcv_info.R_Datatype.ToString() + SPChar +
                                      r_user_field.ToString() + SPChar +
                                      rcv_info.R_MSG + SPChar + '0' + SPChar;
                    // trcode, pnt, rec_data( trcode | datatype | dataseq | msgcd | 0 )
                    DataEventArgs args = new DataEventArgs(rcv_info.R_TrCode, (int)pnt, SendData, rcv_info);
                    //쓰레드 세이프 방식
                    if (RtfGlobal.SessionManager.IsShutDown == false)
                    {
                        rtfDataAgent.ThreadSafe_DataRecvEvent(this, args);
                    }
                }
            }

            //데이터 수신이 완료되면 사용가능 상태로
            if (bFinished)
            {
                SetSessionReadyState(false);
            }
        }

        public void SetSessionReadyState(bool bError)
        {
            TRItem tritem = GetAttachedTRItem();
            if (tritem != null)
            {
                uint nSeq = tritem.nSeq;
                this.state = RtfSessionState.ready_attach;

                if (bError)
                {
                    Debug.WriteLine(string.Format("*** TR 처리에러 nSeq = {0} TRCode = {1} ***", nSeq, tritem.strTrCode));
                }
                else
                {
                    Debug.WriteLine(string.Format("*** TR 처리완료 nSeq = {0} TRCode = {1} ***", nSeq, tritem.strTrCode));
                }

                DettachTRItem(tritem);

                //세션이 대기 상태가 되었음을 알림
                if (type == RtfSessionType.small || type == RtfSessionType.large)
                {
                    //                    if (bConnected)  // 세션이 접속되있는 상태일때만 대기상태로
                    //                    {
                    ReadyEventArgs args = new ReadyEventArgs(nSeq, type);
                    ReadyEvent(this, args);
                    //                    }
                }
            }
        }

        private object tritemLocker = new object();

        //할당 가능한지 여부
        public bool IsSessionReady()
        {
            lock (tritemLocker)
            {
                if (bConnected && m_tritem == null)
                {
                    return true;
                }
                return false;
            }
        }

        long m_last_receive_time = 0;
        public bool CheckRequestPolling()
        {
            lock (tritemLocker)
            {
                //20130129
                // 대기 상태이고 최종수신 시간으로 부터 30초 초과시 폴링
                if (bConnected && m_tritem == null)
                {
                    TimeSpan ts = new TimeSpan(DateTime.Now.Ticks - m_last_receive_time);
                    Debug.WriteLine(string.Format("{0} 마지막 수신후 경과시간:{1}", session_name, ts.TotalSeconds));
                    if (ts.TotalSeconds >= RtfSessionManager.POLLING_SECONDS)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public void AttachTRItem(TRItem tritem)
        {
            lock (tritemLocker)
            {
                /*
                if (this._tritem != null)
                {
                    Debug.WriteLine("tritem!=null 구조적 오류 발생!!");
                    return;
                }
                */
                this.m_tritem = tritem;
                tritem.status = TRItemStatus.attached;
                string log = string.Format("({0}) 세션 {1} 에 TRITEM 할당됨 seq={2}, tr_code={3}, send_data=\"{4}\"", type, id, tritem.nSeq, tritem.strTrCode, tritem.strSendData);
                Debug.WriteLine(log);

                //소켓으로 데이터 전송요청
                SendDataToSocket(tritem);
            }
        }

        private TRItem DettachTRItem()
        {
            lock (tritemLocker)
            {
                TRItem old_tritem = m_tritem;
                m_tritem = null;
                return old_tritem;
            }
        }

        private void DettachTRItem(TRItem tritem)
        {
            lock (tritemLocker)
            {
                if (m_tritem == tritem)
                {
                    m_tritem = null;
                }
            }
        }

        public TRItem GetAttachedTRItem()
        {
            lock (tritemLocker)
            {
                return m_tritem;
            }
        }

        private void SendDataToSocket(TRItem tritem)
        {
            //tritem 정보를 사용하여 데이터를 전송
            //void Data_Send(int PtrCode, int PDest_Way, int PClient_Handle, int Puser_feild, int Pdata_type, int PNext_Len, string PUid, string PInputData);
            if (bConnected)
            {
                this.state = RtfSessionState.ready_request;
                //핸들값에 nSeq값이 전송된다.
                if (tritem.inType == 2)
                {//byte 전송..
                    m_async_socket.Data_Send(Convert.ToInt32(tritem.strTrCode),    //
                    tritem.nSvrNo,            // Dest_way
                    (int)tritem.nSeq,         // TRItem 시퀀스
                    tritem.nDataSeq,          // 유저데이터
                    tritem.nDataType,         // 0:일반 1:대용량
                    0,                        // 넥스트사이즈
                    "",                       // 유저아이디( Data_Send 에서 자동으로 채워줌 )
                    tritem.bytSendData,       // 전송데이터 
                    "");                      // 종료구분 '0'정상 '1'강제종료 '2'강제종료완료
                }
                else
                {//string 전송..
                    m_async_socket.Data_Send(Convert.ToInt32(tritem.strTrCode),    //
                                tritem.nSvrNo,            // Dest_way
                                (int)tritem.nSeq,         // TRItem 시퀀스
                                tritem.nDataSeq,          // 유저데이터
                                tritem.nDataType,         // 0:일반 1:대용량
                                0,                        // 넥스트사이즈
                                "",                       // 유저아이디( Data_Send 에서 자동으로 채워줌 )
                                tritem.strSendData,       // 전송데이터 
                                "");                      // 종료구분 '0'정상 '1'강제종료 '2'강제종료완료
                }
            }
        }

        public void SendToServerNextData()
        {
            SendToServerNextData(this.last_rcv_info);
        }

        public void SendToServerNextData(CommRcvInfo rcv)
        {
            if (m_async_socket != null && rcv != null)
            {
                this.state = RtfSessionState.ready_next;
                m_async_socket.Data_Send(rcv.R_TrCode, rcv.R_DestWay, rcv.R_Handle, rcv.R_UserFeild + 1, rcv.R_Datatype, 0, rcv.R_UserID, rcv.R_NextStr, "");
            }
        }

        //20130725 [5] 기존 TRINFO를 재사용하여 데이터 전송
        public void SendToServerNextDataEx(int SvrNo, byte[] ByteSndData)
        {
            if (SvrNo == 10004)
            {//file 전송시 마지막 데이터..
                this.last_rcv_info.R_DestWay = 10004;
            }
            else
            {
                this.last_rcv_info.R_DestWay = 0;
            }
            SendToServerNextDataEx(this.last_rcv_info, ByteSndData);
        }

        //20130725 [6] 기존 TRINFO를 재사용하여 데이터 전송
        public void SendToServerNextDataEx(CommRcvInfo rcv, byte[] ByteSndData)
        {
            if (m_async_socket != null && rcv != null)
            {
                this.state = RtfSessionState.ready_request;
                m_async_socket.Data_Send(rcv.R_TrCode, rcv.R_DestWay, rcv.R_Handle, rcv.R_UserFeild + 1, rcv.R_Datatype, 0, rcv.R_UserID, ByteSndData, "");
            }
        }

        public void SendToServerServiceExitTR()
        {
            if (m_async_socket != null && this.last_rcv_info != null)
            {
                this.state = RtfSessionState.ready_exit;
            }
            //SendToServerServiceExitTR(this.last_rcv_info); <== 서버로 보내지마.. 
        }

        public void SendToServerServiceExitTR(CommRcvInfo rcv)
        {
            if (m_async_socket != null && rcv != null)
            {
                this.state = RtfSessionState.ready_exit;
                m_async_socket.Data_Send(rcv.R_TrCode, rcv.R_DestWay, rcv.R_Handle, rcv.R_UserFeild + 1, rcv.R_Datatype, 0, rcv.R_UserID, "", "1");
            }
        }

        public void RequestDataToServer(ushort wTR, List<string> requestCodes, int hWnd, bool bRegist)
        {
            if (type == RtfSessionType.push)
            {
                m_async_socket.RequestRTDataToServer(wTR, requestCodes, hWnd, bRegist);
            }
        }

        /*
         void Comcon_OnData(int R_Evnt, int R_TrCode, int R_DestWay, int R_Handle, int R_UserFeild, int R_Datatype, string R_UserID, int R_NextSize, string R_MSG, string R_NextStr, string R_Data,
             int R_Client_Rtn1, int R_Client_Rtn2, int R_Client_Rtn3, string R_KillGbn)
         {
             //20121024 메시지 수신

             switch (R_Evnt)
             {
                 case 1:// File Data....
                     Debug.WriteLine(string.Format(session_name + " [ 소켓에서 데이터 받음 ] TR코드={0}, 데이터={1}...", R_TrCode, R_Data.Substring(0, Math.Min(64, R_Data.Length))));
                     CommRcvInfo rcv = new CommRcvInfo();
                     rcv.R_Evnt = R_Evnt;
                     rcv.R_TrCode = R_TrCode;
                     rcv.R_DestWay = R_DestWay;
                     rcv.R_Handle = R_Handle;
                     rcv.R_UserFeild = R_UserFeild;
                     rcv.R_Datatype = R_Datatype;
                     rcv.R_UserID = R_UserID;
                     rcv.R_NextSize = R_NextSize;
                     rcv.R_MSG = R_MSG;
                     rcv.R_NextStr = R_NextStr;
                     rcv.R_Data = R_Data;
                     rcv.R_Client_Rtn1 = R_Client_Rtn1;
                     rcv.R_Client_Rtn2 = R_Client_Rtn2;
                     rcv.R_Client_Rtn3 = R_Client_Rtn3;
                     rcv.R_KillGbn = R_KillGbn;
                     OnRcvIQData(rcv);
                     break;
                 case 2:// Connect
                     Debug.WriteLine(session_name + " [ Socket Connected! ]");
                     //20130107 재접속중 플래그를 해제한다.
                     _bReConnectByDequeueProc = false;
                     _nReConnectFailedCount = 0;

                     //로그인 세션인 경우 접속되었음을 알린다.
                     if (IsPushSession())
                     {
                         //세션매니저에 푸쉬접속 통보
                         RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.push_connected);
                     }
                     else
                     {
                         //세션매니저에 TR접속 통보
                         RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.connected);
                     }
                     //RequestRTData();
                     break;
                 case 3:// Disconnect
                     //20121005 접속종료시 처리
                     Debug.WriteLine(session_name + " [ Socket Disconnected! ]");

                     //20121018 처리중인 tr이 있다면 전송실패를 날려준다.

                     //처리중인 tr을 버리고 대기상태로 만든다.
                     SetSessionReadyState(true);

                     //세션매니저에 접속종료 통보
                     RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.disconnected);
                     break;
                 case 4:// Connect Failure
                     //20130107 5번까지는 재접속이 수행되도록 한다.
                     _nReConnectFailedCount++;
                     if (_nReConnectFailedCount < 5)
                     {
                         _bReConnectByDequeueProc = false;
                     }

                     Debug.WriteLine(session_name + " [ Socket Connect Failure! ]");
                     //세센매니저에 접속실패 통보
                     RtfGlobal.SessionManager.OnMainFrameConnect(this, SessionManagerEvent.connect_failure);
                     break;
             }
         }
         */
    }

    public enum RtfSessionState
    {
        ready_attach = 0,       //할당을 대기
        ready_request,      //첫번째 서버 응답을 대기
        ready_next,         //넥스트 서버 응답을 대기
        ready_exit,         //강제종료 서버 응답을 대기
        recieved,           //서버 응답 수신
    }

    public enum RtfSessionType
    {
        //미지정
        none = 0,
        //일반 TR
        small = 1,
        //대용량 TR
        large = 2,
        //푸쉬(실시간시세)
        push = 3
    }

    public class CommRcvInfo
    {
        public int R_Evnt;
        public int R_TrCode;
        public int R_DestWay;
        public int R_Handle;
        public int R_UserFeild;
        public int R_Datatype;
        public string R_UserID;
        public int R_NextSize;
        public string R_MSG;
        public string R_NextStr;
        public string R_Data;
        //20120907 추가
        public int R_Client_Rtn1;
        public int R_Client_Rtn2;
        public int R_Client_Rtn3;
        public string R_KillGbn;
        //20130614 RAWDATA 추가
        public byte[] R_RawData;
    }

    public class ReadyEventArgs : EventArgs
    {
        public uint nSeq;
        public RtfSessionType type;
        public ReadyEventArgs(uint nSeq, RtfSessionType type)
        {
            this.nSeq = nSeq;
            this.type = type;
        }
    }
}
