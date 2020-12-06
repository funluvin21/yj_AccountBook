using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections;
//using CommSocketMngrLib;
using System.Threading;
using System.Net;


// 2013/10/22 세션 자동접속을 수동접속으로 변경
// 120초(2분)동안 응답없을시 수동접속 메시지 재연결여부 
// 확인 : 재연결 취소 : 메인프레임으로 알림

namespace AccountBook.Class
{
    public delegate void SessionManagerEventHandler(object sender, SessionManagerEventArgs e);
    //public partial class RtfComponent : Component
    //public partial class RtfSessionManager : Control
    public partial class RtfSessionManager
    {
        public const string category = "SessionManager";

        public event SessionManagerEventHandler OnSessionManagerEvent;
        
        List<RtfSession> listSessionS = new List<RtfSession>();   //단건 조회용 세션리스트
        List<RtfSession> listSessionL = new List<RtfSession>();   //대용량용 세션리스트
        RtfSession SessionPush = null;                            //푸쉬용 세션
        //RtfSession SessionLogin = null;                           //로그인용 세션

        Dictionary<int, object> mapDataAgent = new Dictionary<int, object>();//데이터를 요청한 DataAgent 맵
        Dictionary<uint, TRItem> mapTRItem = new Dictionary<uint, TRItem>(); //생성된 mapTRItem 맵 

        Queue<TRItem> readyQueueS = new Queue<TRItem>(); //대기큐-단건 조회용
        Queue<TRItem> readyQueueL = new Queue<TRItem>(); //대기큐-대용량 조회용

        private object lockDequeue = new object();
        private object lockMapTRItem = new object();

        //모니터링 기능 <START>

        //모니터링 기능 <E_N_D>

        //20121018 세션로긴 기능 추가
        public bool bLoginFinished = false;

        //20121016 세션복구 기능 추가
        //어플리케이션 종료 처리
        private bool _bShutDownProc = false;
        public bool IsShutDown
        {
            get { return _bShutDownProc; }
        }

        //20130129 세션카운트
        //public static int SESSION_COUNT = 5;
        public static int SESSION_COUNT = 1;

        //20130129 세션매니저용 타이며
        System.Threading.Timer m_timer;
        public static int POLLING_CHECK_DUETIME = 60; //60초 간격으로 각 세션들을 폴링 전송 여부를 체크
        public static double POLLING_SECONDS = 120;//최종수신으로부터 120초(2분) 경과시 폴링 전송

        // 2013/10/22 세션 자동접속을 수동접속으로 변경 
        public int m_pollingTag = 0;


        public RtfSessionManager()
        {
            //저용량용 세션 N개 생성
            int nSessionCountSmall = 0;
            for (int i = 0; i < nSessionCountSmall; i++)
            {
                listSessionS.Add(new RtfSession(this, RtfSessionType.small, "저용량세션" + i.ToString()));
            }

            //대용량용 세션 N개 생성
            int nSessionCountLarge = Math.Max(1, SESSION_COUNT);
            for (int i = 0; i < nSessionCountLarge; i++)
            {
                listSessionL.Add(new RtfSession(this, RtfSessionType.large, "대용량세션" + i.ToString()));
            }

            //푸쉬 세션
            SessionPush = new RtfSession(this, RtfSessionType.push, "실시간세션");
            SessionPush.m_async_socket.OnPushData += new RtfAsyncSocket.RtfAsyncSocket_OnPushDataEventHandler(SessionPush_OnPushData);

            //SessionLogin = new RtfSession(this, RtfSessionType.login, "로그인세션");
        }

        //***********************************************
        //이벤트 처리
        //***********************************************
        //<이벤트 처리> RtfDataAgent Disposing시 공통 처리 루틴
        //서버에 요청 대기중인 데이터와 응답 받은 데이터를 처리할 데이터에이전트의 유효성을 판별하기 위해서 사용한다.
        public void SessionManager_DataAgentDisposed(object sender, EventArgs e)
        {
            Debug.WriteLine("SessionManager_DataAgentDisposed", category);
            //관리중인 큐에 해당 데이터에이전트가 있을 경우 처리해준다.
            UnregDataAgent(sender);
        }
        //호스트 IP 가져오기
        public string GetDnsToIp(string hostName)
        {
            string strIp = string.Empty;

            //// 호스트/도메인명에서 IP 알아내기

            // 인터넷 호스트명 정보 얻기
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);

            Console.WriteLine(hostEntry.HostName);
            foreach (IPAddress ip in hostEntry.AddressList)
            {
                Console.WriteLine(ip);
                strIp = ip.ToString();
            }

            // 로컬 호스트명 정보 얻기
            string hostname = Dns.GetHostName();
            IPHostEntry localhost = Dns.GetHostEntry(hostname);

            return strIp;
        }

        //푸쉬서버 접속
        public void ConnectPushServer(string strIP, string strPort)
        {
            SessionPush.ConnectToServer(strIP, strPort);
        }

        //세션 매니저 종료
        public void ShutDownSessionManager()
        {
            bLoginFinished = false;
            _bShutDownProc = true;
            // 2013/10/22 세션 자동접속을 수동접속으로 변경 
            m_pollingTag = 0;
            //폴링 타이머 정지
            PollingTimerStop();

            //대기큐 클리어
            readyQueueS.Clear();
            readyQueueL.Clear();

            for (int i = 0; i < listSessionS.Count; i++)
            {
                listSessionS[i].ShutDownSession();
            }

            for (int i = 0; i < listSessionL.Count; i++)
            {
                listSessionL[i].ShutDownSession();
            }

            SessionPush.ShutDownSession();
            //SessionLogin.ShutDownSession();
        }

        private string strIP = "";
        private string strPort = "";

        //HTS 메인 접속
        public void MainFrameConnectServer(string strIP, string strPort)
        {
            if (bLoginFinished)
            {
                return;
            }

            _bShutDownProc = false;

            this.strIP = strIP;
            this.strPort = strPort;
            //SessionLogin.ConnectToServer(strIP, strPort);
            listSessionL[0].ConnectToServer(strIP, strPort);
        }

        delegate void EventCallBack(object sender, SessionManagerEventArgs e);
        void ThreadSafe_OnSessionManagerEvent(object sender, SessionManagerEventArgs e)
        {
            if (OnSessionManagerEvent != null)
            {
                //if (RtfGlobal.MainForm.InvokeRequired)
                //{
                //    if (_bShutDownProc == false)
                //    {
                //        EventCallBack d = new EventCallBack(ThreadSafe_OnSessionManagerEvent);
                //        RtfGlobal.MainForm.Invoke(d, new object[] { sender, e });
                //    }
                //}
                //else
                //{
                    OnSessionManagerEvent(sender, e);
                //}
            }
        }

        public void PollingTimerStart()
        {
            if (m_timer == null)
            {
                m_timer = new System.Threading.Timer(new TimerCallback(SessionManager_OnTimer));
            }
            PollingTimerUpdate();
        }

        void PollingTimerUpdate()
        {
            if (m_timer != null)
            {
                m_timer.Change(POLLING_CHECK_DUETIME * 1000, Timeout.Infinite);
            }
        }

        void PollingTimerStop()
        {
            if (m_timer != null)
            {
                m_timer.Dispose();
                m_timer = null;
            }
        }

        void SessionManager_OnTimer(object state)
        {


            if (_bShutDownProc == false)
            {
                //폴링 체크시 디큐를 막는다.
                PollingTimerStop();
                lock (lockDequeue)
                {
                    // 2013/10/22 세션 자동접속을 수동접속으로 변경 
                    m_pollingTag++;
                    //                    Debug.WriteLine(string.Format("폴링카운트 : {0}", m_pollingTag.ToString()));
                    // 폴링 데이타가 내려오지 않는것으로 보아 문제가 있는것으로 체크
                    // 2번 polling 했는데 응답이 없으면 끊어버리고 재연결 메시지 뛰움
                    if (m_pollingTag > 2)
                    {
                        // 메인프레임으로 알림
                        // 메인프레임에서 판단(재연결할지, 취소할지)
                        ThreadSafe_OnSessionManagerEvent(this, new SessionManagerEventArgs(SessionManagerEvent.reconnectyn));
                        return;
                    }
                    //각 세션들의 폴링 상태를 체크한다.
                    for (int i = 0; i < listSessionL.Count; i++)
                    {
                        RtfSession session = listSessionL[i];

                        //20130130 세션 접속이 끊어진 경우 재접속한다.
                        if (session.bConnected == false)
                        {
                            //                           session.ReconnectToServerBySessionDisconnected();
                        }
                        else
                        {
                            //폴링 TR을 전송할지 여부를 체크한다.
                            //                            if (session.CheckRequestPolling() == true)
                            //                            {
                            // 2013/12/30 세션이 할당 가능한지 체크를 해주자.....
                            // 대용량 data 연속 조회시 유효성 확보 91101 침범가능
                            // 연속조회 중간에 서버에 서비스 종료 TR을 전송 문제 발생 (SendToServerServiceExitTR)
                            if (session.IsSessionReady())
                            {
                                byte[] buffData = null;
                                TRItem tritem_polling = new TRItem(1, "91101", 0, 1, 0, "", buffData, 0);
                                session.AttachTRItem(tritem_polling);
                            }
                            //                            }
                        }
                    }
                }
                //                m_pollingTag++;
                PollingTimerStart();
                //폴링 타이머 재가동
                //                PollingTimerUpdate();
            }
        }

        public void reConnectAllSession(bool bTry)
        {
            int i = 0;
            for (i = 0; i < listSessionL.Count; i++)
            {
                listSessionL[i].ShutDownSession();
            }
            SessionPush.ShutDownSession();

            if (bTry)
            {
                for (i = 0; i < listSessionL.Count; i++)
                {
                    RtfSession session = listSessionL[i];
                    session.ReconnectToServerBySessionDisconnected();
                }
                //ConnectInfo connectInfo = RtfGlobal.Instance.GetConnectInfo();
               // ConnectPushServer(connectInfo._PushServerIP, connectInfo._PushServerPORT); //"10.1.121.112", "7700"
            }
            //            m_pollingTag = 0;
        }
        //메인 접속 성공
        public void OnMainFrameConnect(RtfSession session, SessionManagerEvent smng_event)
        {
            if (session.IsPushSession())
            {
                //푸쉬 접속시...
                if (smng_event == SessionManagerEvent.push_connected)
                {
                    //메인 프레임에 접속 성공 이벤트를 알린다
                    ThreadSafe_OnSessionManagerEvent(this, new SessionManagerEventArgs(SessionManagerEvent.push_connected));
                }
            }
            else
            {
                //로그인시 최초 접속 성공전일때...
                if (bLoginFinished == false)
                {
                    if (smng_event == SessionManagerEvent.connected)
                    {
                        //최초로 접속이 성공한 순간..
                        bLoginFinished = true;

                        //각 세션들을 접속
                        ConnectSessionsToServer(strIP, strPort);

                        //폴링 타이머 시작
                        PollingTimerStart();
                    }

                    if (smng_event == SessionManagerEvent.connected || smng_event == SessionManagerEvent.connect_failure)
                    {
                        //세션메니저의 이벤트를 메인에 통보한다. //최초 접속성공시
                        // 로그인시 접속성공/실패/등등...
                        ThreadSafe_OnSessionManagerEvent(this, new SessionManagerEventArgs(smng_event));
                    }
                }
                else
                {
                    //로그인후... 접속종료시...
                    if (smng_event == SessionManagerEvent.disconnected)
                    {
                        //전체 세션 접속 종료시...
                        bool bNotifyAllDisconnected = true;
                        for (int i = 0; i < listSessionL.Count; i++)
                        {
                            RtfSession cur_session = listSessionL[i];
                            if (cur_session.bConnected == true)
                            {
                                bNotifyAllDisconnected = false;
                                break;
                            }
                        }
                        if (bNotifyAllDisconnected)
                        {
                            ThreadSafe_OnSessionManagerEvent(this, new SessionManagerEventArgs(smng_event));
                        }
                    }
                }

                //20130130 접속이벤트 발생시 현재 대기중인 TRITEM처리
                if (this._bShutDownProc == false)
                {
                    //if (smng_event == SessionManagerEvent.connected || smng_event == SessionManagerEvent.connect_failure)
                    if (smng_event == SessionManagerEvent.connected)
                    {
                        // 2013/10/22 세션 자동접속을 수동접속으로 변경 
                        m_pollingTag = 0; // 폴링 카운트를 초기화 
                        //대기큐에서 유효한 TRItem을 세션에 할당
                        DequeueReadyQueue(true);
                    }
                }
            }

        }

        //개별 세션 접속
        public void ConnectSessionsToServer(string strIP, string strPort)
        {
            for (int i = 0; i < listSessionS.Count; i++)
            {
                listSessionS[i].ConnectToServer(strIP, strPort);
            }

            for (int i = 0; i < listSessionL.Count; i++)
            {
                listSessionL[i].ConnectToServer(strIP, strPort);
            }
        }

        //데이터 요청시에 등록
        public void RegDataAgent(object objToReg)
        {
            int key = objToReg.GetHashCode();
            if (mapDataAgent.ContainsKey(key) == false)
            {
                mapDataAgent.Add(key, objToReg);

                //RTFDataAgent dataAgent = objToReg as RTFDataAgent;
                //object objTarget = dataAgent.GetTargetInfo();
                Debug.WriteLine("DataAgent 등록성공", category);
            }
        }

        //화면 종료시에 등록 해제
        public void UnregDataAgent(object objToUnreg)
        {
            int key = objToUnreg.GetHashCode();
            if (mapDataAgent.ContainsKey(key))
            {
                mapDataAgent.Remove(key);
                Debug.WriteLine("DataAgent 등록해제", category);
            }

            //20130107 DataAgent 해제시 세션에서 사용중이면 해당세션 재접속
            if (_bShutDownProc == false)
            {
                List<RtfSession> dst_listSession = listSessionL;
                for (int i = 0; i < dst_listSession.Count; i++)
                {
                    RtfSession session = dst_listSession[i];
                    TRItem tritem = session.GetAttachedTRItem();
                    if (tritem != null)
                    {
                        if (tritem.nDataAgentHash == key)
                        {
                            //20130130 조회중 화면 종료시 세션재접속
                            session.ReconnectToServerByScreenClose();
                        }
                    }
                }
            }

        }

        //데이터 송수신시 TRItem과 연결된 DataAgent의 유효성 체크
        public bool IsValidDataAgent(int nHashCode)
        {
            return mapDataAgent.ContainsKey(nHashCode);
        }

        public bool GetValidDataAgent(int nHashCode, out object dstDataAgent)
        {
            dstDataAgent = null;
            if (IsValidDataAgent(nHashCode))
            {
                if (mapDataAgent.TryGetValue(nHashCode, out dstDataAgent))
                {
                    return true;
                }
            }
            return false;
        }

        //mapTRItem 맵에 TRItem 추가
        public void AddTRItem(TRItem tritem)
        {
            lock (lockMapTRItem)
            {
                mapTRItem.Add(tritem.nSeq, tritem);
            }
        }

        // mapTRItem 맵에서 TRItem 삭제
        //삭제되는 시점 : 1. 할당된 경우 데이터수신완료 또는 강제 처리종료시( 취소, 무효한 DataAgent )
        //                2. 대기중인 경우 세션에 할당시 실패한 경우( 취소, 무효한 DataAgent )
        public void RemoveTRItem(uint nSeq)
        {
            lock (lockMapTRItem)
            {
                if (mapTRItem.ContainsKey(nSeq))
                {
                    mapTRItem.Remove(nSeq);
                    Debug.WriteLine("TRItem 삭제", category);
                }
            }
        }

        public TRItem GetTRItem(uint nSeq)
        {
            lock (lockMapTRItem)
            {
                if (mapTRItem.ContainsKey(nSeq))
                {
                    return mapTRItem[nSeq];
                }
                return null;
            }
        }

        public void SetTRItemCancel(TRItem tritem)
        {
            //대기중 취소, 세션에 할당된 상태에서 취소가 생길수 있다.
            TRItemStatus statusBefore = tritem.status;
            if (statusBefore == TRItemStatus.ready || statusBefore == TRItemStatus.attached)
            {
                tritem.status = TRItemStatus.canceled;
            }
        }

        public void RequestNextDataManual(object objDataAgent)
        {
            List<RtfSession> dst_listSession = listSessionL;

            //현재 사용가능한 세션이 있는지 검색
            for (int i = 0; i < dst_listSession.Count; i++)
            {
                RtfSession session = dst_listSession[i];
                TRItem tritem = session.GetAttachedTRItem();
                if (tritem != null)
                {
                    if (tritem.nDataAgentHash == objDataAgent.GetHashCode())
                    {
                        session.SendToServerNextData();
                    }
                }
            }
        }

        //20130725 [4] 대상 DataAgent를 처리중인 세션을 찾는다.
        public void RequestNextDataManualEx(int SvrNo, object objDataAgent, byte[] ByteSndData)
        {
            List<RtfSession> dst_listSession = listSessionL;

            //현재 사용가능한 세션이 있는지 검색
            for (int i = 0; i < dst_listSession.Count; i++)
            {
                RtfSession session = dst_listSession[i];
                TRItem tritem = session.GetAttachedTRItem();
                if (tritem != null)
                {
                    //세션을 찾음
                    if (tritem.nDataAgentHash == objDataAgent.GetHashCode())
                    {
                        session.SendToServerNextDataEx(SvrNo, ByteSndData);
                    }
                }
            }
        }

        public void RequestExitDataManual(object objDataAgent)
        {
            List<RtfSession> dst_listSession = listSessionL;

            //현재 사용가능한 세션이 있는지 검색
            for (int i = 0; i < dst_listSession.Count; i++)
            {
                RtfSession session = dst_listSession[i];
                TRItem tritem = session.GetAttachedTRItem();
                if (tritem != null)
                {
                    if (tritem.nDataAgentHash == objDataAgent.GetHashCode())
                    {
                        //수신 대기인 상태
                        if (session.state == RtfSessionState.recieved)
                        {
                            session.SendToServerServiceExitTR();
                        }
                    }
                }
            }
        }

        public void Data_Cancel_BySeq(object objDataAgent, uint nSeq)
        {
            lock (lockMapTRItem)
            {
                TRItem tritem;
                if (mapTRItem.TryGetValue(nSeq, out tritem))
                {
                    //동일한 DataAgent일때만 가능하다
                    if (tritem.nDataAgentHash == objDataAgent.GetHashCode())
                    {
                        SetTRItemCancel(tritem);
                    }
                }
            }
        }

        public void Data_Cancel_ByTrCode(object objDataAgent, string strTrCode)
        {
            lock (lockMapTRItem)
            {
                //동일한 tr코드 전부 취소
                Dictionary<uint, TRItem>.Enumerator enumerator = mapTRItem.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    TRItem tritem = enumerator.Current.Value;
                    //동일한 DataAgent일때만 가능하다
                    if (tritem.nDataAgentHash == objDataAgent.GetHashCode())
                    {
                        if (tritem.strTrCode == strTrCode)
                        {
                            SetTRItemCancel(tritem);
                        }
                    }
                }
            }
        }

        public void Data_Cancel_ByDataAgent(object objDataAgent)
        {
            lock (lockMapTRItem)
            {
                //동일한 DataAgent일 경우 전부 취소
                Dictionary<uint, TRItem>.Enumerator enumerator = mapTRItem.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    TRItem tritem = enumerator.Current.Value;
                    //동일한 DataAgent일때만 가능하다
                    if (tritem.nDataAgentHash == objDataAgent.GetHashCode())
                    {
                        SetTRItemCancel(tritem);
                    }
                }
            }
        }

        //Data_Request("3200", 0, 1, 1, str_total);
        public uint Data_Request(short inType, string strTrCode, int nSvrNo, int nDataSeq, int nDataType, string strSendData, byte[] bytSendData, object objDataAgent)
        {
            // DataAgent 등록 ( 화면 종료시에 DataAgent의 유효성을 체크하는데 사용함 )
            // 대기중 또는 처리중인 TRItem을 소유하고 있는 DataAgent가 무효한 경우 자동 취소 처리된다.
            RegDataAgent(objDataAgent);

            // 화면 또는 RTFDataAgent 에서 데이터요청이 들어온경우 처리
            // 일반데이터는 일반 큐에 추가
            // 대용량데이터는 대용량 큐에 추가

            bool bLarge = true;
            /*
            bool bLarge = false;
            if (nDataType == 1) //0:일반 1:대용량
            {
                bLarge = true;
            }
            */

            //<TRItem> 생성 시작
            //TRItem 생성 후 전역으로 관리되는 맵에 추가 < 키:시퀀스, 밸류:TRItem >
            TRItem tqd = new TRItem(inType, strTrCode, nSvrNo, nDataSeq, nDataType, strSendData, bytSendData, objDataAgent.GetHashCode());
            AddTRItem(tqd);
            //<TRItem> 생성 종료

            //대기큐에 추가 시작
            Queue<TRItem> dst_queue = null;
            dst_queue = (bLarge) ? readyQueueL : readyQueueS;
            dst_queue.Enqueue(tqd);
            string log = string.Format("{0} 대기큐에 추가. 대기큐 사이즈={1}, TRCode={2}", bLarge ? "Large" : "Small", dst_queue.Count, tqd.strTrCode);
            Debug.WriteLine(log);
            //대기큐에 추가 종료

            //대기큐에서 유효한 TRItem을 세션에 할당
            DequeueReadyQueue(bLarge);

            return tqd.nSeq;
        }

        //대기큐에서 TRItem을 하나씩 얻어와서 처리한다.
        public void DequeueReadyQueue(bool bLarge)
        {
            //20130130 셧다운 상태면 더이상 처리하지 않는다.
            if (_bShutDownProc)
            {
                return;
            }

            //쓰레드 사용시 DequeueReadyQueue 블럭이 동시에 실행되므로 주의..
            //work_session에 이미 TRItem 할당중인 세션이 다시 할당되는 문제 발생..
            //크리티컬 섹션으로 블럭킹해서 처리.
            lock (lockDequeue)
            {
                Queue<TRItem> dst_queue = (bLarge) ? readyQueueL : readyQueueS;
                List<RtfSession> dst_listSession = (bLarge) ? listSessionL : listSessionS;

                //대기큐가 비어있다면 종료
                if (dst_queue.Count < 1)
                {
                    return;
                }

                //현재 사용가능한 세션이 있는지 검색
                RtfSession work_session = null;
                for (int i = 0; i < dst_listSession.Count; i++)
                {
                    RtfSession session = dst_listSession[i];
                    //현재 세션이 사용 가능한 상태면
                    if (session.IsSessionReady())
                    {
                        //스레드 오류 방지 : 가능상태가 되자마자 불가능 상태로 만들어 버리는 방법( 크리티컬세션 미사용시 오류 최소화 )
                        work_session = session;
                        break;
                    }

                    //20130130 세션이 접속이 끊어진 상태면 재접속 <자동재접속 사용안함>
                    /*
                    if (_bShutDownProc == false)
                    {
                        if (session.bConnected == false)
                        {
                            session.ReconnectToServerBySessionDisconnected();
                        }
                    }
                    */
                }

                //사용가능한 세션이 없으면 종료
                if (work_session == null)
                {
                    return;
                }

                //사용가능한 세션이 있다면 ... 
                //큐에서 tritem을 dequeue
                TRItem tritem_attach = null;
                while (dst_queue.Count > 0 && tritem_attach == null)
                {
                    //일단 맨앞에꺼 하나를 꺼내온다.
                    TRItem tritem = dst_queue.Dequeue();

                    string log = string.Format("***** {0} 대기큐에서 삭제.. 대기큐 사이즈={1} TRItem nSeq={2} 상태={3}", bLarge ? "Large" : "Small", dst_queue.Count, tritem.nSeq, tritem.status);
                    Debug.WriteLine(log);

                    //TRItem의 대상 DataAgent의 유효성을 체크
                    if (IsValidDataAgent(tritem.nDataAgentHash))
                    {
                        //현재 대기 상태인 TR아이템만 세션에 할당한다.
                        if (tritem.status == TRItemStatus.ready)
                        {
                            tritem_attach = tritem;
                            break;
                        }
                    }

                    //세션 할당에 실패한 경우 TRItem을 관리 맵에서 삭제
                    RemoveTRItem(tritem.nSeq);
                }

                //세션에 tritem을 등록
                if (tritem_attach != null)
                {
                    work_session.AttachTRItem(tritem_attach);
                }
            }
        }

        public void OnSessionReady(object sender, ReadyEventArgs e)
        {
            if (_bShutDownProc) // 
            {
                Debug.WriteLine("_bShutDownProc: true");
                return;
            }
            /*
            RtfSession session = sender as RtfSession;

            if (session != null)
            {
                //접속이 끊어져서 대기상태가 된 경우
                if (session.bConnected == false)
                {
                    Debug.WriteLine(string.Format("세션명={0}, SessionDataRcvEvent={1}", session.session_name, sender.GetHashCode().ToString()));
                    return;
                }
            }
             */
            /*
            if (session != null)
            {
                //접속이 끊어져서 대기상태가 된 경우
                if (session.bConnected == false)
                {
                    //20130107 셧다운 동작이 아니면 해당 세션을 재접속 한다.
                    if (_bShutDownProc == false)
                    {
                        session.ReconnectToServer();
                    }
                }
            }
            */

            //세션에 할당된 TRITEM 처리가 완료되 세션이 대기상태로 전환된 경우 발생

            //세션 할당에 실패한 경우 TRItem을 관리 맵에서 삭제
            RemoveTRItem(e.nSeq);

            bool bLarge = (e.type == RtfSessionType.large) ? true : false;
            DequeueReadyQueue(bLarge);

            //Debug.WriteLine("현재 쓰레드아이디:" + Thread.CurrentThread.ManagedThreadId.ToString(),"SessionDataRcvEvent");
            //Debug.WriteLine("SessionDataRcvEvent " + sender.GetHashCode().ToString());

            //쓰레드에 안전한 방식
            //RtfGlobal.MainForm.SendSystem("setdata", "");
            /*
            if (RtfGlobal.MainForm.InvokeRequired)
            {
                Debug.WriteLine("RtfGlobal.MainForm.InvokeRequired = true", "SessionDataRcvEvent");
            }
            */

        }

        //*****************************************
        //리얼데이터 처리 추가 2012.07.16
        //*****************************************
        private static uint s_RTID = 1;
        private Dictionary<uint, RT_ID> m_mapID = new Dictionary<uint, RT_ID>();//리얼 요청 건별로 관리 < 시퀀스, RT_ID >
        private Dictionary<string, RT_TR> m_mapTR = new Dictionary<string, RT_TR>();//동일TR+동일코드의 윈도우리스트 관리 < TR+종목코드, RT_TR >

        //리얼데이터 수신
        void SessionPush_OnPushData(ushort wTR, string strCode, string[] arrData)
        {
            //Debug.WriteLine("SessionPush_OnPushData");
            if (IsShutDown)
            {
                return;
            }

            string strBaseKey = string.Format("{0:D3}", wTR);
            string strKey = strBaseKey + strCode;

            RT_TR pRTTR;
            if (m_mapTR.TryGetValue(strKey, out pRTTR))
            {
                for (int i = 0; i < pRTTR.arrWindow.Count; i++)
                {
                    int hWnd = pRTTR.arrWindow[i];
                    object dstDataAgent;
                    if (GetValidDataAgent(hWnd, out dstDataAgent))
                    {
                        RTFDataAgent rtfDataAgent = dstDataAgent as RTFDataAgent;
                        if (rtfDataAgent != null)
                        {
                            PushEventArgs args = new PushEventArgs((int)wTR, strCode, arrData);
                            rtfDataAgent.ThreadSafe_PushEvent(SessionPush, args);
                        }
                    }
                }
            }

        }

        //리얼데이터 요청
        public uint RequestRTData(ushort wTR, string strCodes, object objDataAgent)
        {
            // DataAgent 등록 ( 화면 종료시에 DataAgent의 유효성을 체크하는데 사용함 )
            // 대기중 또는 처리중인 TRItem을 소유하고 있는 DataAgent가 무효한 경우 자동 취소 처리된다.
            RegDataAgent(objDataAgent);

            int hWnd = objDataAgent.GetHashCode();
            uint nReqID = 0;
            unchecked
            {
                nReqID = s_RTID++;
            }

            RT_ID pRTID = null;

            //이미 등록된 RT_ID 이면 오류상황
            if (m_mapID.ContainsKey(nReqID))
            {
                return 0;
            }
            else
            {
                pRTID = new RT_ID();
                m_mapID.Add(nReqID, pRTID);
            }

            pRTID.dwID = nReqID;
            pRTID.wTR = wTR;
            pRTID.hWnd = hWnd;

            string[] arrCodes = strCodes.Split(',');
            foreach (string code in arrCodes)
            {
                if (code != "")
                {
                    pRTID.arrCode.Add(code);
                }
            }

            List<string> requestCodes = new List<string>();

            // 뉴스, 공시는 종목코드 없음.
            string strTRKey = string.Format("{0:D3}", pRTID.wTR);
            if (pRTID.wTR == 200 || pRTID.wTR == 300)
            {
                RT_TR pRTTR;
                //이미 서버에 요청한 경우 핸들만 추가
                if (m_mapTR.TryGetValue(strTRKey, out pRTTR))
                {
                    pRTTR.arrWindow.Add(pRTID.hWnd);
                }
                //서버에 요청하지 않은 경우
                else
                {
                    pRTTR = new RT_TR();
                    pRTTR.wTR = pRTID.wTR;
                    pRTTR.strCode = "";
                    pRTTR.arrWindow.Add(pRTID.hWnd);
                    m_mapTR.Add(strTRKey, pRTTR);
                    // 뉴스, 공시는 종목코드 없이 요청한다
                    requestCodes.Add("");
                }
            }
            else
            {
                foreach (string strCode in pRTID.arrCode)
                {
                    string strKey = strTRKey + strCode;
                    RT_TR pRTTR;
                    //이미 서버에 요청한 경우 핸들만 추가
                    if (m_mapTR.TryGetValue(strKey, out pRTTR))
                    {
                        pRTTR.arrWindow.Add(pRTID.hWnd);
                    }
                    //서버에 요청하지 않은 경우
                    else
                    {
                        pRTTR = new RT_TR();
                        pRTTR.wTR = pRTID.wTR;
                        pRTTR.strCode = "";
                        pRTTR.arrWindow.Add(pRTID.hWnd);
                        m_mapTR.Add(strKey, pRTTR);

                        // 2012.07.17. 일단 1001 -> 001 로 하자
                        string strReqCode = strCode;
                        /*
                        if ( wTR == 100 )
                        {
                            if ( strCode.Length == 4 )//20120717 
                            {
                                strReqCode = strCode.Substring(1, 3);
                            }
                        }
                        */
                        requestCodes.Add(strReqCode);
                    }
                }
            }

            //서버로 요청해야하는 종목코드가 있는경우
            if (requestCodes.Count > 0)
            {
                SessionPush.RequestDataToServer(wTR, requestCodes, hWnd, true);
            }

            // 리턴값이 0 이면 리얼요청 오류
            return nReqID;
        }

        public void CloseRTData(uint nReqID, object objDataAgent)
        {
            //========================================================================
            // ID로 RT정보의 포인터를 얻는다.
            //========================================================================
            uint nID = nReqID;
            RT_ID pRTID = null;
            if (m_mapID.TryGetValue(nID, out pRTID) == false)
            {
                return;
            }
            ushort wTR = pRTID.wTR;
            int hWnd = pRTID.hWnd;
            int nCnt = pRTID.arrCode.Count;

            List<string> requestCodes = new List<string>();

            // 뉴스, 공시는 종목코드 없음.
            string strTRKey = string.Format("{0:D3}", pRTID.wTR);
            if (wTR == 200 || wTR == 300)
            {
                RT_TR pRTTR;
                //이미 서버에 요청한 경우 핸들만 삭제
                if (m_mapTR.TryGetValue(strTRKey, out pRTTR))
                {
                    pRTTR.arrWindow.Remove(hWnd);
                    //핸들 리스트의 개수가 0 이면 맵에서 삭제
                    if (pRTTR.arrWindow.Count < 1)
                    {
                        m_mapTR.Remove(strTRKey);
                        // 뉴스, 공시는 종목코드 없이 요청한다
                        requestCodes.Add("");
                    }
                }
            }
            else
            {
                foreach (string strCode in pRTID.arrCode)
                {
                    string strKey = strTRKey + strCode;
                    RT_TR pRTTR;
                    //이미 서버에 요청한 경우 핸들만 추가
                    if (m_mapTR.TryGetValue(strKey, out pRTTR))
                    {
                        pRTTR.arrWindow.Remove(hWnd);
                        //핸들 리스트의 개수가 0 이면 맵에서 삭제
                        if (pRTTR.arrWindow.Count < 1)
                        {
                            m_mapTR.Remove(strKey);
                            // 뉴스, 공시는 종목코드 없이 요청한다
                            requestCodes.Add(strCode);
                        }
                    }
                }
            }

            //서버로 요청해야하는 종목코드가 있는경우
            if (requestCodes.Count > 0)
            {
                SessionPush.RequestDataToServer(wTR, requestCodes, hWnd, false);
            }

            //맵에서 삭제
            pRTID.arrCode.Clear();
            m_mapID.Remove(nID);
        }
    }

    public enum SessionManagerEvent
    {
        connected = 1,
        disconnected,
        connect_failure,
        push_connected,
        // 2013/10/22 세션 자동접속을 수동접속으로 변경
        reconnectyn, // 재연결 여부 메인프레임에 알림
    }

    public class SessionManagerEventArgs : EventArgs
    {
        public SessionManagerEvent nEvent = 0;
        public SessionManagerEventArgs(SessionManagerEvent nEvent)
        {
            this.nEvent = nEvent;
        }
    }

    //개별 화면에서 요청한 리얼정보를 저장
    public class RT_ID
    {
        public uint dwID;   //시퀀스
        public ushort wTR;   //TR번호
        public int hWnd;    //요청한 핸들
        public List<string> arrCode = new List<string>();//요청한 종목리스트
    }

    //동일한 TR의 동일한 종목일 경우 해당 윈도우 리스트로 관리
    public class RT_TR
    {
        public ushort wTR;   //TR번호
        public string strCode;//종목코드
        public List<int> arrWindow = new List<int>();//동일TR 동일종목을 요청한 윈도우리스트
    }

    public enum TRItemStatus
    {
        // Queue에서 대기중
        ready = 0,
        // Session에서 처리중
        attached = 1,
        // 사용자요청에의해 취소
        canceled = 2,
        // 처리완료
        finished = 3
    }

    public class TimeLog
    {
        public long tick_Send = 0;
        public long tick_Receive = 0;
        public TimeLog(long send)
        {
            tick_Send = send;
        }
        public void SetReceiveTime(long receive)
        {
            tick_Receive = receive;
        }
        public TimeSpan GetElpased()
        {
            return new TimeSpan(tick_Receive - tick_Send);
        }
    }

    public class TRItem
    {
        private static uint nStaticSeq = 0;       //내부 시퀀스

        private uint _nSeq;
        public uint nSeq
        {
            get { return _nSeq; }
        }

        public TRItemStatus status;

        //연결된 DataAgent의 해쉬코드
        public int nDataAgentHash;

        //기존 데이터 스펙
        public short inType;
        public string strTrCode;
        public int nSvrNo;
        public int nDataSeq;
        public int nDataType;
        public string strSendData;
        public byte[] bytSendData;

        //시간 로그 추가
        public List<TimeLog> lstTimeLog = new List<TimeLog>();
        public int AddSendTime(DateTime dt)
        {
            lstTimeLog.Add(new TimeLog(dt.Ticks));
            return lstTimeLog.Count;
        }
        public void SetReceiveTime(DateTime dt)
        {
            if (lstTimeLog.Count > 0)
            {
                lstTimeLog[lstTimeLog.Count - 1].SetReceiveTime(dt.Ticks);
            }
        }
        public string GetLastTimeSpan()
        {
            if (lstTimeLog.Count > 0)
            {
                TimeLog tl = lstTimeLog[lstTimeLog.Count - 1];
                return tl.GetElpased().ToString();
            }
            return "Error";
        }
        public string GetLastDelayTimeSpan()
        {
            if (lstTimeLog.Count > 1)
            {
                long prev_receive = lstTimeLog[lstTimeLog.Count - 2].tick_Receive;
                long last_send = lstTimeLog[lstTimeLog.Count - 1].tick_Send;
                TimeSpan ts = new TimeSpan(last_send - prev_receive);
                return ts.ToString();
            }
            return string.Empty;
        }
        public string GetTotalTimeSpan()
        {
            if (lstTimeLog.Count > 0)
            {
                long first_send = lstTimeLog[0].tick_Send;
                long last_receive = lstTimeLog[lstTimeLog.Count - 1].tick_Receive;
                TimeSpan ts = new TimeSpan(last_receive - first_send);
                return ts.ToString();
            }
            return "Error";
        }

        //개별 생성 방지
        private TRItem() { }

        public TRItem(short inType, string strTrCode, int nSvrNo, int nDataSeq, int nDataType, string strSendData, byte[] bytSendData, int nDataAgentHash)
        {
            unchecked
            {
                _nSeq = ++nStaticSeq;
            }

            this.status = TRItemStatus.ready;
            this.nDataAgentHash = nDataAgentHash;

            this.inType = inType;
            this.strTrCode = strTrCode;
            this.nSvrNo = nSvrNo;
            this.nDataSeq = nDataSeq;
            this.nDataType = nDataType;
            this.strSendData = strSendData;
            this.bytSendData = bytSendData;
        }
    }
}
