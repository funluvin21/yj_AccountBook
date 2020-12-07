using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace AccountBook.Class
{
    public enum RtfSockType
    {
        tran = 0,
        push = 1,
    }

    public partial class RtfAsyncSocket : Component
    {
        public delegate void RtfAsyncSocket_OnDataEventHandler(int R_Evnt, int R_TrCode, int R_DestWay, int R_Handle, int R_UserFeild, int R_Datatype, string R_UserID, int R_NextSize, string R_MSG, string R_NextStr, string R_Data, int R_Client_Rtn1, int R_Client_Rtn2, int R_Client_Rtn3, string R_KillGbn);
        public delegate void RtfAsyncSocket_OnTranDataEventHandler(int R_Evnt, int R_TrCode, int R_DestWay, int R_Handle, int R_UserFeild, int R_Datatype, string R_UserID, int R_NextSize, string R_MSG, string R_NextStr, string R_Data);
        public delegate void RtfAsyncSocket_OnPushDataEventHandler(ushort wTR, string strCode, string[] arrData);

        public string m_sock_name = string.Empty;
        public bool m_bUnix = false;        //유닉스서버 여부(유닉스:바이트오더링필요)
        public RtfSockType m_sock_type = 0; //0:조회 1:리얼(유닉스:바이트오더링필요)

        MemoryStream m_msRcv = new MemoryStream(1024 * 1024 * 12);//10MB
        int m_nTotalRcvSize = 0;

        //부모 세션
        RtfSession m_session = null;

        //public event RtfAsyncSocket_OnDataEventHandler OnData;
        //public event RtfAsyncSocket_OnTranDataEventHandler OnTranData;
        public event RtfAsyncSocket_OnPushDataEventHandler OnPushData;

        private Socket m_socket = null;
        private string m_IP = string.Empty;
        private string m_PORT = string.Empty;
        private IPEndPoint m_IPEndPoint = null;
        private ProtocolType m_ProtocolType = ProtocolType.Tcp;
        public event SessionEventHandler OnConnect;
        public event SessionEventHandler OnClose;
        public event SessionEventHandler OnReceive;
        public event SessionEventHandler OnSend;

        public bool Connected
        {
            get
            {
                //                lock (socketLocker) // 2013/10/18 세션 끊길때 죽는 원인 주석처리 lock을 여기서 걸 필요가 없음
                //                {
                return (m_socket == null) ? false : m_socket.Connected;
                //                }
            }
        }

        public RtfAsyncSocket(RtfSession session, RtfSockType sock_type, string sock_name)
        {
            this.m_session = session;
            this.m_sock_type = sock_type;
            if (this.m_sock_type == RtfSockType.push)
            {
                this.m_bUnix = true;
            }
            this.m_sock_name = sock_name;
        }

        protected override void Dispose(bool disposing)
        {
            this.m_msRcv.Close();
            this.m_session = null;
            base.Dispose(disposing);
        }

        public void Connect(string IP, string PORT)
        {
            IPAddress ipaddress;
            if (IPAddress.TryParse(IP, out ipaddress) == false)
            {
                EndConnect(false);
                return;
            }

            int port;
            if (Int32.TryParse(PORT, out port) == false)
            {
                EndConnect(false);
                return;
            }

            try
            {
                IPEndPoint ipe = new IPEndPoint(ipaddress, port);
                if (ipe != null)
                {
                    //IP / PORT 입력
                    m_IP = IP;
                    m_PORT = PORT;
                    m_ProtocolType = ProtocolType.Tcp;
                    m_IPEndPoint = ipe;

                    //신규서버에 신규소켓으로 접속시도
                    BeginConnect();
                }
            }
            catch (System.Exception)
            {
                m_IP = string.Empty;
                m_PORT = string.Empty;
                m_IPEndPoint = null;
                EndConnect(false);
            }
        }

        public void Reconnect()
        {
            //20130130 재접속 요청 상태로 만든다 <자동재접속 삭제>
            //SetReconnect();
            //기존서버에 신규소켓으로 접속시도
            BeginConnect();
        }

        //20130130 <자동재접속 삭제>
        /*
        private bool _bReConnectProcessing = false;
        private int _nReConnectFailedCount = 0;
        private object retryLocker = new object();
        public void SetReconnect()
        {
            lock (retryLocker)
            {
                if (_bReConnectProcessing == false)
                {
                    _bReConnectProcessing = true;
                    _nReConnectFailedCount = 0;
                }
                else
                {
                    return;
                }
            }
        }
        public void ResetReconnect()
        {
            lock (retryLocker)
            {
                if (_bReConnectProcessing)
                {
                    _bReConnectProcessing = false;
                    _nReConnectFailedCount = 0;
                }
                else
                {
                    return;
                }
            }
        }
        public void TryReconnect()
        {
            bool bBeginConnect = false;
            lock (retryLocker)
            {
                //재접속 진행중인데 접속실패시 5번까지 재시도 한다.
                if (_bReConnectProcessing)
                {
                    _nReConnectFailedCount++;
                    if (_nReConnectFailedCount >= 5)
                    {
                        //재접속 상태를 해제
                        _bReConnectProcessing = false;
                        _nReConnectFailedCount = 0;
                    }
                    else
                    {
                        //접속을 재시도 한다
                        bBeginConnect = true;
                    }
                }
                else
                {
                    return;
                }
            }
            //재접속 시작
            if (bBeginConnect)
            {
                BeginConnect();
            }
        }
        */

        public void Close()
        {
            //현재 할당된 소켓 닫기 시작
            if (m_socket == null)
                return;
            BeginCloseSocket(GetAttachedSocket());
        }

        public void SendData(byte[] buffer)
        {
            //전송용 소켓 체크
            Socket send_socket = GetAttachedSocket();
            if (send_socket == null || send_socket.Connected == false)
            {
                return;
            }

            SocketAsyncEventArgs async_send = new SocketAsyncEventArgs();
            try
            {
                async_send.UserToken = send_socket;
                async_send.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                async_send.SetBuffer(buffer, 0, buffer.Length);
                bool willRaiseEvent = send_socket.SendAsync(async_send);
                //즉시 응답이 발생한 경우
                if (!willRaiseEvent)
                {
                    ProcessSend(async_send);
                }
            }
            catch (Exception ex)
            {
                async_send.Dispose();
                Console.WriteLine("SendAsync Error : {0}", ex.Message);
            }
            finally
            {
                buffer = null;
            }
        }

        private object socketLocker = new object();
        private void AttachSocket(Socket socket)
        {
            lock (socketLocker)
            {
                Socket old_socket = DettachSocket();
                //소켓 설정
                m_socket = socket;
                //누적 패킷 사이즈 초기화
                m_nTotalRcvSize = 0;
                //이전 소켓 닫기 시작
                BeginCloseSocket(old_socket);
            }
        }

        private Socket DettachSocket()
        {
            lock (socketLocker)
            {
                Socket old_socket = m_socket;
                m_socket = null;
                return old_socket;
            }
        }

        private Socket GetAttachedSocket()
        {
            lock (socketLocker)
            {
                return m_socket;
            }
        }

        private bool IsAttachedSocket(Socket socket)
        {
            lock (socketLocker)
            {
                if (m_socket != null && m_socket == socket)
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsConnectProcessing = false;
        private object connectLocker = new object();
        private void BeginConnect()
        {
            lock (connectLocker)
            {
                if (IsConnectProcessing)
                {
                    Console.WriteLine("Connect Error : IsConnectProcessing is true");
                    return;
                }
                IsConnectProcessing = true;
            }

            //IPEndPoint 체크
            if (m_IPEndPoint == null)
            {
                //20130130 재접속 상태를 리셋 <자동재접속 삭제>
                //ResetReconnect(); 
                //접속 종료 처리
                EndConnect(false);
                return;
            }

            //신규 소켓 생성후 비동기 접속 시도
            Socket connect_socket = new Socket(m_IPEndPoint.AddressFamily, SocketType.Stream, m_ProtocolType);

            //소켓 할당
            AttachSocket(connect_socket);

            //비동기 접속 이벤트 생성
            SocketAsyncEventArgs async_connect = new SocketAsyncEventArgs();
            async_connect.UserToken = connect_socket;
            async_connect.RemoteEndPoint = m_IPEndPoint;
            async_connect.Completed += new EventHandler<SocketAsyncEventArgs>(this.IO_Completed);

            try
            {
                bool willRaiseEvent = connect_socket.ConnectAsync(async_connect);
                //즉시 응답이 발생한 경우
                if (!willRaiseEvent)
                {
                    ProcessConnect(async_connect);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connect Error : {0}", ex.Message);
                async_connect.Dispose();
                EndConnect(false);
            }
        }

        private void EndConnect(bool bConnected)
        {
            //접속결과 통지
            FireOnConnect(bConnected);

            //접속중 상태 해지
            IsConnectProcessing = false;

            //20130130 접속 결과에 따른 처리 <자동재접속 삭제>
            /*
            if (bConnected)
            {
                //접속 성공시 재접속 설정을 해제한다.
                ResetReconnect();
            }
            else
            {
                //접속 실패시 재접속 요청을 한다.
                TryReconnect();
            }
            */
        }

        //접속 결과 통보
        private void FireOnConnect(bool bConnected)
        {
            if (OnConnect != null)
            {
                OnConnect(this, new SessionEventArgs(bConnected));
            }
        }

        //접속종료 시작 처리
        private void BeginCloseSocket(Socket socket)
        {
            if (socket == null)
            {
                return;
            }

            bool bCallEndCloseSocket = true;
            if (socket.Connected)
            {
                // close the socket associated with the client
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                // throws if client process has already closed
                catch (Exception ex)
                {
                    Console.WriteLine("Shutdown Error : {0}", ex.Message);
                }

                SocketAsyncEventArgs async_disconnect = new SocketAsyncEventArgs();
                try
                {
                    async_disconnect.UserToken = socket;
                    async_disconnect.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                    async_disconnect.DisconnectReuseSocket = false;

                    bool willRaiseEvent = socket.DisconnectAsync(async_disconnect);
                    //즉시 응답이 발생한 경우
                    if (!willRaiseEvent)
                    {
                        ProcessDisconnect(async_disconnect);
                    }
                    bCallEndCloseSocket = false;
                }
                catch (System.Exception ex)
                {
                    async_disconnect.Dispose();
                    bCallEndCloseSocket = true;
                    Console.WriteLine("DisconnectAsync Error : {0}", ex.Message);
                }
            }

            if (bCallEndCloseSocket)
            {
                //접속해제 (이벤트 통지)
                EndCloseSocket(socket, true);
            }
        }

        //접속종료 마지막 처리
        private void EndCloseSocket(Socket socket, bool bFireOnCloseEventIfAttachedSocket)
        {
            if (socket == null) { return; }
            lock (socketLocker)
            {
                //대상 소켓 닫기
                socket.Close();
                //현재 할당된 소켓이면 할당을 해제한다
                if (m_socket == socket)
                {
                    m_socket = null;
                    //조건이 true 이면 현재 할당된 소켓의 종료 이벤트를 통지한다.
                    if (bFireOnCloseEventIfAttachedSocket)
                    {
                        FireOnClose();
                    }
                }
            }
        }

        //접속 결과 통보
        private void FireOnClose()
        {
            if (OnClose != null)
            {
                OnClose(this, new SessionEventArgs());
            }
        }

        //수신 대기 시작
        private void ReadyToReceive(Socket rcv_socket, SocketAsyncEventArgs async_rcv)
        {
            //수신용 소켓 체크
            if (rcv_socket == null || rcv_socket.Connected == false)
            {
                return;
            }

            if (async_rcv == null)
            {
                //수신 이벤트 생성
                async_rcv = new SocketAsyncEventArgs();
                async_rcv.UserToken = rcv_socket;
                async_rcv.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);
                //수신 버퍼 할당
                byte[] buffRead = new byte[65536];
                async_rcv.SetBuffer(buffRead, 0, buffRead.Length);
            }

            try
            {
                bool willRaiseEvent = rcv_socket.ReceiveAsync(async_rcv);
                //즉시 응답이 발생한 경우
                if (!willRaiseEvent)
                {
                    ProcessReceive(async_rcv);
                }
            }
            catch (System.Exception ex)
            {
                async_rcv.Dispose();
                Console.WriteLine("SendData Error : {0}", ex.Message);
            }
        }

        void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            Socket socket = e.UserToken as Socket;

            //////////////////////////////////////////////////////////////////////////
            //이전 소켓에서 받은 메시지 : 무시한다
            if (IsAttachedSocket(socket) == false)
            {
                e.Dispose();
                return;
            }
            //////////////////////////////////////////////////////////////////////////

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    ProcessConnect(e);
                    break;
                case SocketAsyncOperation.Disconnect:
                    ProcessDisconnect(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
            }

        }

        public int m_LocalAddress = 0;
        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            Socket socket = (Socket)e.UserToken;
            bool bConnected = socket.Connected;
            if (bConnected)
            {
                //IP주소 입력
                IPEndPoint ipe = socket.LocalEndPoint as IPEndPoint;
                //                m_LocalAddress = (int)ipe.Address.Address;

                //수신 대기
                ReadyToReceive(socket, null);
            }
            else
            {
                //접속해제 (이벤트 통지 안함)
                EndCloseSocket(socket, false);
            }

            //접속 결과를 알려준다.
            EndConnect(bConnected);

            //사용완료후 클리어
            e.Dispose();
        }

        private void ProcessDisconnect(SocketAsyncEventArgs e)
        {
            Socket socket = (Socket)e.UserToken;

            //접속해제 (이벤트 통지)
            EndCloseSocket(socket, true);

            //사용완료후 클리어
            e.Dispose();
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            Socket socket = (Socket)e.UserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                //전송완료 이벤트 통지
                if (OnSend != null)
                {
                    MemoryStream msRcv = new MemoryStream(e.Buffer, 0, e.BytesTransferred);
                    byte[] buffRcv = msRcv.ToArray();
                    OnSend(this, new SessionEventArgs(buffRcv));
                }
            }
            else
            {
                //송신 에러시 대상 소켓 종료
                BeginCloseSocket(socket);
            }

            //사용완료후 클리어
            e.Dispose();
        }

        void ProcessReceivePush(SocketAsyncEventArgs e, byte[] pData, int nSize)
        {
            //메인 종료시 푸쉬 수신데이터 처리하지 않는다.
            if (RtfGlobal.SessionManager.IsShutDown == true)
            {
                return;
            }

            object objStruct;

            int szICHD_RT = Marshal.SizeOf(typeof(ICHD_RT));
            int szICHD_RT_CODE = Marshal.SizeOf(typeof(ICHD_RT_CODE));

            BytesToStructure(pData, 4, out objStruct, typeof(ICHD_RT));
            ICHD_RT hdRT = (ICHD_RT)objStruct;
            BytesToStructure(pData, 4 + szICHD_RT, out objStruct, typeof(ICHD_RT_CODE));
            ICHD_RT_CODE hdCode = (ICHD_RT_CODE)objStruct;

            if (m_bUnix)
            {
                hdRT.nDLen = IPAddress.NetworkToHostOrder(hdRT.nDLen);
                hdRT.nDCnt = IPAddress.NetworkToHostOrder(hdRT.nDCnt);
                hdCode.nRepCnt = IPAddress.NetworkToHostOrder(hdCode.nRepCnt);
            }

            int nszData = hdRT.nDLen;

            //반복 건수( 지수에만 적용 )
            int nRepCnt = hdCode.nRepCnt;

            int nContentSize = nszData - szICHD_RT_CODE;//서버에서 주는값인데 오류가 있어서 아래의 계산값을 사용한다.

            int nContentStartIndex = 4 + szICHD_RT + szICHD_RT_CODE;
            int nCalcContentSize = nSize - nContentStartIndex; //수신한 사이즈에서 컨텐츠 시작점을 빼면 컨트츠 사이즈가 나온다.

            //오류 확인용
            if (nContentSize != nCalcContentSize)
            {
                nContentSize = nCalcContentSize;
            }

            const byte PC_INDEX_FID = 0x36;
            const byte PC_NEWS_FID = 0x4a;
            const byte PC_NOTICE_FID = 0x55;

            ushort nTrCode = 0;
            switch (hdRT.ucPacketID)
            {
                case PC_INDEX_FID://업종지수
                    nTrCode = 100;
                    break;
                case PC_NEWS_FID://뉴스
                    nTrCode = 200;
                    break;
                case PC_NOTICE_FID://공시
                    nTrCode = 300;
                    break;
            }

            string strHdCode = "";
            List<string> listData = new List<string>();

            //데이터 파싱
            if (nTrCode == 100)
            {
                //지수 데이터파싱 : 체결데이터인 경우 리얼TR에 여러개의 데이터가 포함될수 있다.
                strHdCode = A2W(hdCode.ucCode);
                strHdCode = strHdCode.Trim();

                int szST_INDEX = Marshal.SizeOf(typeof(ST_INDEX));
                if (nRepCnt <= 0)
                {
                    nRepCnt = 1;
                }

                //버퍼사이즈와 반복횟수가 일치하는지 검사해보자
                int nCalcRepCnt = nContentSize / szST_INDEX;
                if (nCalcRepCnt != nRepCnt)
                {
                    Debug.WriteLine("반복횟수가 일치하지 않는다!");
                    nRepCnt = nCalcRepCnt;
                }

                for (int iLoop = 0; iLoop < nRepCnt; iLoop++)
                {
                    int nCurrentContentIndex = nContentStartIndex + (iLoop * szST_INDEX);
                    BytesToStructure(pData, nCurrentContentIndex, out objStruct, typeof(ST_INDEX));
                    ST_INDEX stIndex = (ST_INDEX)objStruct;
                    if (m_bUnix)
                    {
                        stIndex.nTime = IPAddress.NetworkToHostOrder(stIndex.nTime);

                        stIndex.nClose = IPAddress.NetworkToHostOrder(stIndex.nClose);
                        stIndex.nChange = IPAddress.NetworkToHostOrder(stIndex.nChange);
                        stIndex.nChgRate = IPAddress.NetworkToHostOrder(stIndex.nChgRate);
                        stIndex.nOpen = IPAddress.NetworkToHostOrder(stIndex.nOpen);
                        stIndex.nHigh = IPAddress.NetworkToHostOrder(stIndex.nHigh);
                        stIndex.nLow = IPAddress.NetworkToHostOrder(stIndex.nLow);

                        stIndex.n64TickVol = IPAddress.NetworkToHostOrder(stIndex.n64TickVol);
                        stIndex.n64Volume = IPAddress.NetworkToHostOrder(stIndex.n64Volume);
                        stIndex.n64Amount = IPAddress.NetworkToHostOrder(stIndex.n64Amount);

                    }

                    string szCode = A2W(stIndex.szCode);

                    listData.Clear();
                    listData.Add(szCode);                           //[0]종목코드
                    listData.Add(stIndex.nClose.ToString());        //[1]현재가
                    //string strGiho = Encoding.Default.GetString(stIndex.chChangeGiho);
                    string strGiho = new string((char)stIndex.chChangeGiho, 1);
                    listData.Add(strGiho);  //[2]대비기호
                    //listData.Add(stIndex.chChangeGiho.ToString());  //[2]대비기호
                    listData.Add(stIndex.nChange.ToString());       //[3]전일대비
                    listData.Add(stIndex.n64Volume.ToString());     //[4]거래량

                    //Debug.WriteLine(String.Format("<지수> 헤더코드={0} 종목코드={1}, 현재가={2}, 기호={3}, 대비={4}, 거래량={5}",
                    //strHdCode, szCode, stIndex.nClose, stIndex.chChangeGiho, stIndex.nChange, stIndex.n64Volume));

                    //푸쉬데이터 화면에 전송
                    //Debug.WriteLine(string.Format("RT Received. TrCode={0}, Code={1}", nTrCode, strCode));
                    string[] arrData = listData.ToArray();
                    OnPushData(nTrCode, strHdCode, arrData);
                }
            }
            else if (nTrCode == 200)
            {
                //뉴스 데이터 파싱
                BytesToStructure(pData, nContentStartIndex, out objStruct, typeof(ST_NEWS));
                ST_NEWS stNews = (ST_NEWS)objStruct;
                if (m_bUnix)
                {
                    stNews.nDate = IPAddress.NetworkToHostOrder(stNews.nDate);
                    stNews.nTime = IPAddress.NetworkToHostOrder(stNews.nTime);
                }
                string strTitle = Encoding.Default.GetString(stNews.szTitle, 0, stNews.szTitle.Length).Trim();

                listData.Add(stNews.nDate.ToString());
                listData.Add(stNews.nTime.ToString());
                listData.Add(strTitle);

                //Debug.WriteLine(String.Format("<뉴스> 날짜={0} 시간={1} 타이틀={2}", stNews.nDate, stNews.nTime, strTitle));

                //푸쉬데이터 화면에 전송
                //Debug.WriteLine(string.Format("RT Received. TrCode={0}, Code={1}", nTrCode, strHdCode));
                string[] arrData = listData.ToArray();
                OnPushData(nTrCode, strHdCode, arrData);
            }
            else if (nTrCode == 300)
            {
                //공시 데이터 파싱
                BytesToStructure(pData, nContentStartIndex, out objStruct, typeof(ST_NOTICE));
                ST_NOTICE stNotice = (ST_NOTICE)objStruct;
                if (m_bUnix)
                {
                    stNotice.nDate = IPAddress.NetworkToHostOrder(stNotice.nDate);
                    stNotice.nTime = IPAddress.NetworkToHostOrder(stNotice.nTime);
                }
                string strTitle = Encoding.Default.GetString(stNotice.szTitle, 0, stNotice.szTitle.Length).Trim();

                listData.Add(stNotice.nDate.ToString());
                listData.Add(stNotice.nTime.ToString());
                listData.Add(strTitle);

                //Debug.WriteLine(String.Format("<공시> 날짜={0} 시간={1} 타이틀={2}", stNotice.nDate, stNotice.nTime, strTitle));

                //푸쉬데이터 화면에 전송
                //Debug.WriteLine(string.Format("RT Received. TrCode={0}, Code={1}", nTrCode, strHdCode));
                string[] arrData = listData.ToArray();
                OnPushData(nTrCode, strHdCode, arrData);
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            Socket socket = e.UserToken as Socket;
            //int nPrevRcvSize = nTotalRcvSize;
            //데이터 정상 수신시
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                //현재TR 수신사이즈 누적
                m_nTotalRcvSize += e.BytesTransferred;

                m_msRcv.Write(e.Buffer, 0, e.BytesTransferred);

                int nPacketSize = BitConverter.ToInt32(m_msRcv.GetBuffer(), 0);

                //바이트오더링
                if (m_bUnix)
                {
                    nPacketSize = IPAddress.NetworkToHostOrder(nPacketSize);
                }

                int nRawPacketSize = nPacketSize + 4;

                //패킷사이즈 미만으로 받았다면 다시 리시브하기까지 대기
                if (m_nTotalRcvSize < nRawPacketSize)
                {
                    //Read data sent from the server
                    //수신완료후 다시 수신대기 상태로 만든다.
                    ReadyToReceive(socket, e);
                    return;
                }

                //패킷사이즈 이상 수신완료한 경우
                int nOverPacketSize = 0;
                if (m_nTotalRcvSize > nRawPacketSize)
                {
                    nOverPacketSize = m_nTotalRcvSize - nRawPacketSize;
                }

                //수신버퍼에서 포인터를 얻어온다.
                byte[] pData = m_msRcv.GetBuffer();

                //*************************************************************
                //TR 데이터 가공
                //*************************************************************
                if (m_sock_type == RtfSockType.tran)
                {
                    //20121018 데이터수신 완료시간 기록
                    DateTime rcv_time = DateTime.Now;
                    /*
                    TRItem cur_tritem = _session._tritem;
                    if (cur_tritem != null)
                    {
                        cur_tritem.SetReceiveTime(DateTime.Now);
                        Debug.WriteLine(string.Format("전송에서 수신까지 ... 소요시간 {0}", cur_tritem.GetLastTimeSpan()));
                    }
                    */
                    //버퍼에서 헤더를 추출한다
                    object objStruct;
                    BytesToStructure(pData, 0, out objStruct, typeof(ST_HEADER));
                    ST_HEADER stHeader = (ST_HEADER)objStruct;

                    //Debug.WriteLine(string.Format(sock_name + " [ OnReceive TRCode = {0}, {1} bytes! ]", stHeader.TrCode, e.BytesTransferred));
                    Debug.WriteLine(string.Format(m_sock_name + " [ OnReceive TRCode = {0}, {1} bytes! ]", stHeader.TrCode, nRawPacketSize));

                    string strNext = "";
                    string strData = "";

                    int szHeader = Marshal.SizeOf(stHeader);

                    //20120907 압축데이터에 넥스트데이터는 포함되지 않는다.
                    int nNextSize = stHeader.Next_KeyLen;
                    strNext = Encoding.Default.GetString(pData, szHeader, stHeader.Next_KeyLen);

                    //20121018 속도 테스트 < 스킵해제 >
                    //20121212 여기서 바로 넥스트 요청할경우
                    // 시퀀스ID로 TRITEM을 얻어온다.
                    // 넥스트사이즈가 0보다 크고, 데이터에이전트가 유효하고, TRITEM이 취소상태가 아닐경우 자동으로 넥스트요청
                    // 데이터에이전트가 자동요청 사용중일경우
                    /*
                    if (nNextSize > 0)
                    {
                        uint nSeq = Convert.ToUInt32(stHeader.Client_Handle);
                        TRItem tritem = RtfGlobal.SessionManager.GetTRItem(nSeq);
                        if (tritem != null)
                        {
                            object dstDataAgent;
                            bool bValidDataAgent = RtfGlobal.SessionManager.GetValidDataAgent(tritem.nDataAgentHash, out dstDataAgent);
                            bool bAutoNextRequest = false;
                            RTFDataAgent rtfDataAgent = dstDataAgent as RTFDataAgent;
                            if (rtfDataAgent != null)
                            {
                                bAutoNextRequest = rtfDataAgent.bAutoNextRequest;
                            }
                            //도중에 취소 요청이 들어온경우...
                            bool bCanceled = (tritem.status == TRItemStatus.canceled) ? true : false;
                            if (bAutoNextRequest && bCanceled == false)
                            {
                                string strNextUserID = Encoding.Default.GetString(stHeader.User_ID);
                                Data_Send(stHeader.TrCode, stHeader.Dest_Way, stHeader.Client_Handle, stHeader.User_Field + 1, stHeader.Data_Type, 0, strNextUserID, strNext, "");
                            }
                        }
                    }
                    */

                    //////////////////////////////////////////////////////////////////////////
                    //20130614 : RAWDATA 포인터
                    byte[] buffRcv;
                    //////////////////////////////////////////////////////////////////////////

                    //압축유무 체크
                    if (stHeader.CompressFlg == '1')
                    {
                        //**************************************************************************************
                        // *** 압축해제 - ZOutputStream 사용 ( 출력용 스트림을 입력한후에 압축된 데이터를 Write함 )
                        // 아웃스트림 - 압축해제된 메모리스트림, 아웃스트림에 압축된 데이터를 씀
                        //**************************************************************************************
                        /*
                        Stopwatch swzip = new Stopwatch();
                        swzip.Start();
                        MemoryStream msDecom = new MemoryStream();//24MB
                        ZOutputStream zout = new ZOutputStream(msDecom);
                        zout.Write(pData, szHeader + nNextSize, nRawPacketSize - szHeader - nNextSize);
                        zout.Flush();
                        int nDecompressSize = (int)msDecom.Length;
                        byte[] buffDecomp = msDecom.GetBuffer();
                        //압축해제 데이터에서 서버에서 준 길이만큼만 변환한다.
                        nDecompressSize = stHeader.Option_len;
                        strData = Encoding.Default.GetString(buffDecomp, 0, nDecompressSize);
                        swzip.Stop();
                        Debug.WriteLine("외부라이브러리 압축해제 소요시간 : " + swzip.Elapsed.ToString());
                        
                        //압축 객체 정리
                        zout.Close();
                        msDecom.Close();
                        */

                        //압축해제 C#버전
                        int nDecompressSize = stHeader.Option_len;
                        MemoryStream msDecomp = new MemoryStream(nDecompressSize);
                        MemoryStream msComp = new MemoryStream(pData, szHeader + nNextSize, nRawPacketSize - szHeader - nNextSize);
                        msComp.Position = 2;
                        DeflateStream decompressStream = new DeflateStream(msComp, CompressionMode.Decompress);
                        int length = 0;
                        byte[] readBuff = new byte[65536];
                        while (true)
                        {
                            length = decompressStream.Read(readBuff, 0, readBuff.Length);
                            if (length > 0)
                            {
                                msDecomp.Write(readBuff, 0, length);
                            }
                            else
                            {
                                break;
                            }
                        }
                        byte[] buffDecomp = msDecomp.GetBuffer();

                        //20130614 압축해제된 버퍼를 지정
                        buffRcv = buffDecomp;

                        try
                        {
                            strData = Encoding.Default.GetString(buffDecomp, 0, (int)msDecomp.Length);
                        }
                        catch
                        {
                        }

                        msComp.Close();
                        msDecomp.Close();
                        decompressStream.Close();

                        Debug.WriteLine(string.Format("압축사이즈={0}, 원본사이즈={1}, 유니코드사이즈={2}", nRawPacketSize - szHeader - nNextSize, nDecompressSize, strData.Length * 2));

                        //20121023
                        GC.Collect();
                    }
                    else
                    {
                        //비압축 데이터
                        //strNext = Encoding.Default.GetString(pData, szHeader, stHeader.Next_KeyLen);
                        //비압축 데이터
                        //strNext = Encoding.Default.GetString(pData, szHeader, stHeader.Next_KeyLen);

                        //20130614 데이터 영역의 포인터를 지정
                        MemoryStream msRcv = new MemoryStream(pData, szHeader + stHeader.Next_KeyLen, nRawPacketSize - szHeader - stHeader.Next_KeyLen);
                        //buffRcv = msRcv.GetBuffer();//<==기존 버퍼 pData재사용하는 방식에서는 error 발생합니다..

                        buffRcv = msRcv.ToArray();
                        //buffRcv = new byte[nRawPacketSize - szHeader - stHeader.Next_KeyLen];
                        //Buffer.BlockCopy(pData, szHeader + stHeader.Next_KeyLen, buffRcv, 0, nRawPacketSize - szHeader - stHeader.Next_KeyLen);

                        try
                        {
                            strData = Encoding.Default.GetString(pData, szHeader + stHeader.Next_KeyLen, nRawPacketSize - szHeader - stHeader.Next_KeyLen);
                        }
                        catch
                        {

                        }
                    }

                    //넥스트요청 < 세션에서 요청함 >
                    //if (stHeader.Next_KeyLen > 0)
                    //{
                    //    SendData(async_send, 3200, 0, stHeader.User_Field++, 1, strNext);
                    //}

                    //*****************************************
                    //수신이벤트 전송
                    //*****************************************
                    //Event 1:tr_data 2:Connect 3:Disconnect
                    string strUserID = Encoding.Default.GetString(stHeader.User_ID);
                    string strMsgCode = Encoding.Default.GetString(stHeader.Msg_cd);

                    string strKillGbn = ((char)stHeader.KillGbn).ToString();

                    //20121024 구버전 메세지 수신
                    /*
                    if (OnData != null)
                    {
                        OnData(1, stHeader.TrCode, stHeader.Dest_Way, stHeader.Client_Handle, stHeader.User_Field, stHeader.Data_Type, strUserID, stHeader.Next_KeyLen, strMsgCode, strNext, strData,
                            stHeader.Client_Rtn1, stHeader.Client_Rtn2, stHeader.Client_Rtn3, strKillGbn);
                    }
                    */

                    //20130129 신버전 메세지 수신
                    //수신완료 이벤트 통지
                    if (OnReceive != null)
                    {
                        CommRcvInfo rcv_info = new CommRcvInfo();
                        rcv_info.R_Evnt = 1;
                        rcv_info.R_TrCode = stHeader.TrCode;
                        rcv_info.R_DestWay = stHeader.Dest_Way;
                        rcv_info.R_Handle = stHeader.Client_Handle;
                        rcv_info.R_UserFeild = stHeader.User_Field;
                        rcv_info.R_Datatype = stHeader.Data_Type;
                        rcv_info.R_UserID = strUserID;
                        rcv_info.R_NextSize = stHeader.Next_KeyLen;
                        rcv_info.R_MSG = strMsgCode;
                        rcv_info.R_NextStr = strNext;
                        rcv_info.R_Data = strData;
                        rcv_info.R_Client_Rtn1 = stHeader.Client_Rtn1;
                        rcv_info.R_Client_Rtn2 = stHeader.Client_Rtn2;
                        rcv_info.R_Client_Rtn3 = stHeader.Client_Rtn3;
                        rcv_info.R_KillGbn = strKillGbn;

                        //20130614 RAWDATA 저장
                        rcv_info.R_RawData = buffRcv;

                        OnReceive(this, new SessionEventArgs(rcv_info, rcv_time));
                    }
                }
                //*************************************************************
                //PUSH 데이터 가공
                //*************************************************************
                else if (m_sock_type == RtfSockType.push)
                {
                    ProcessReceivePush(e, pData, nRawPacketSize);
                }

                //*****************************************
                // 수신버퍼 초기화
                //*****************************************
                if (nOverPacketSize > 0)
                {
                    //초과한 데이터를 수신버퍼 처음으로 복사해준다.
                    Buffer.BlockCopy(m_msRcv.GetBuffer(), nRawPacketSize, m_msRcv.GetBuffer(), 0, nOverPacketSize);
                    m_msRcv.Position = nOverPacketSize;
                    m_nTotalRcvSize = nOverPacketSize;
                }
                else
                {
                    //수신버퍼 초기화 및 수신사이즈 초기화
                    m_msRcv.Position = 0;
                    m_nTotalRcvSize = 0;
                }

                //**************************************************
                //다음데이터 수신을 대기
                //**************************************************
                if (socket.Connected)
                {
                    //수신완료후 다시 수신대기 상태로 만든다.
                    ReadyToReceive(socket, e);
                }
                else
                {
                    Debug.WriteLine("소켓해제");
                }
            }
            else
            {
                //수신 에러시 대상 소켓 종료
                BeginCloseSocket(socket);

                //사용완료후 클리어
                e.Dispose();
            }
        }

        public string A2W(byte[] bytes)
        {
            return Encoding.Default.GetString(bytes);
        }

        //20130129
        public byte[] GetUserID()
        {
            byte[] return_user_id = new byte[8];
            for (int i = 0; i < return_user_id.Length; i++)
            {
                return_user_id[i] = Convert.ToByte(0);
            }
            string strLoginID = RtfGlobal.LoginID;
            byte[] user_id = Encoding.ASCII.GetBytes(strLoginID);//Encoding.Default.GetBytes("00000000");
            Buffer.BlockCopy(user_id, 0, return_user_id, 0, Math.Min(8, user_id.Length));
            return return_user_id;
        }

        public void Data_Send(int PtrCode, int PDest_Way, int PClient_Handle, int Puser_feild, int Pdata_type, int PNext_Len, string PUid, string PInputData, string strKillGbn)
        {
            // 서버로 보낼 데이터를 만든다.
            ST_HEADER header = new ST_HEADER();

            byte[] buffData = Encoding.Default.GetBytes(PInputData);
            int szData = buffData.Length;

            int szHeader = Marshal.SizeOf(header);
            header.DataLen = szHeader - 4 + szData;
            header.TrCode = PtrCode;
            header.Dest_Way = PDest_Way;
            header.Client_Handle = PClient_Handle;
            header.User_Field = Puser_feild;
            header.Data_Type = Pdata_type;
            header.CompressFlg = (byte)'0';
            header.Msg_cd = Encoding.Default.GetBytes("0000");
            header.User_ID = GetUserID();   //20130129 유저아이디 입력
            header.Option_len = szData;

            //20120907 신규추가
            header.KillGbn = (byte)'0';
            if (strKillGbn == "1")
            {
                header.KillGbn = (byte)'1';
            }

            //예비 데이터 1
            header.Client_Rtn1 = 0;
            //예비 데이터 2
            header.Client_Rtn2 = 0;
            //예비 데이터 3 : IP주소 입력
            header.Client_Rtn3 = m_LocalAddress;

            byte[] buffHeader;
            StructToBytes(header, out buffHeader);

            MemoryStream ms = new MemoryStream(buffHeader.Length + buffData.Length);
            ms.Write(buffHeader, 0, buffHeader.Length);
            ms.Write(buffData, 0, buffData.Length);
            byte[] buffer = ms.GetBuffer();

            //20130115 동기 전송
            //m_socket.Send(buffer);

            //비동기 전송
            SendData(buffer);
            /*
            if (async_send != null)
            {
                nSendingTrCode = header.TrCode;
                async_send.SetBuffer(buffer, 0, buffer.Length);
                bool willRaiseEvent = m_socket.SendAsync(async_send);
                if (!willRaiseEvent)
                {
                    ProcessSend(async_send);
                }
            }
            */
        }

        public void Data_Send(int PtrCode, int PDest_Way, int PClient_Handle, int Puser_feild, int Pdata_type, int PNext_Len, string PUid, byte[] PInputData, string strKillGbn)
        {
            // 서버로 보낼 데이터를 만든다.
            ST_HEADER header = new ST_HEADER();

            byte[] buffData = PInputData;//Encoding.Default.GetBytes(PInputData);
            int szData = buffData.Length;

            int szHeader = Marshal.SizeOf(header);
            header.DataLen = szHeader - 4 + szData;
            header.TrCode = PtrCode;
            header.Dest_Way = PDest_Way;
            header.Client_Handle = PClient_Handle;
            header.User_Field = Puser_feild;
            header.Data_Type = Pdata_type;
            header.CompressFlg = (byte)'0';
            header.Msg_cd = Encoding.Default.GetBytes("0000");
            header.User_ID = GetUserID();   //20130129 유저아이디 입력
            header.Option_len = szData;

            //20120907 신규추가
            header.KillGbn = (byte)'0';
            if (strKillGbn == "1")
            {
                header.KillGbn = (byte)'1';
            }

            switch (PDest_Way)
            {//file 전송시 마지막 데이터 처리..
                case 10004:
                    header.Client_Rtn1 = 10004;
                    break;
                default:
                    header.Client_Rtn1 = 0;
                    break;
            }
            /*
            //예비 데이터 1
            header.Client_Rtn1 = 0;
            */
            //예비 데이터 2
            header.Client_Rtn2 = 0;
            //예비 데이터 3 : IP주소 입력
            header.Client_Rtn3 = m_LocalAddress;

            byte[] buffHeader;
            StructToBytes(header, out buffHeader);

            MemoryStream ms = new MemoryStream(buffHeader.Length + buffData.Length);
            ms.Write(buffHeader, 0, buffHeader.Length);
            ms.Write(buffData, 0, buffData.Length);
            byte[] buffer = ms.GetBuffer();

            //비동기 전송
            SendData(buffer);
        }

        const byte PC_CODEAUTO_FID = 0x30;
        const byte REAL_CONTRACT = 0x80;

        //리얼데이터 서버 요청/해제 구현함수
        public void RequestRTDataToServer(ushort wTR, List<string> requestCodes, int hWnd, bool bRegist)
        {
            byte ucMarketFlag = 0;
            if (wTR == 100)
            {
                ucMarketFlag = (byte)'3';
            }
            else if (wTR == 200)
            {
                ucMarketFlag = (byte)'B';
            }
            else if (wTR == 300)
            {
                ucMarketFlag = (byte)'D';
            }
            else
            {
                return;
            }

            short nCodeCount = (short)requestCodes.Count;

            ICHD_IQ hdIQ = new ICHD_IQ();
            ICHD_RTSEND hdRT = new ICHD_RTSEND();
            ICHD_RT_FRM hdFM = new ICHD_RT_FRM();

            byte ucClass = REAL_CONTRACT;

            //빈값채우기
            hdIQ.ucTrCode = new byte[8];
            hdIQ.ucBranchNo = new byte[3];
            hdIQ.ucNextKey = new byte[4];
            hdIQ.ucfiller = new byte[3];
            hdIQ.ucMsgCode = new byte[4];

            hdIQ.hWinID = IPAddress.HostToNetworkOrder(hWnd);
            hdIQ.ucCPflag = 0x00;
            hdIQ.ucPacketID = PC_CODEAUTO_FID;
            hdIQ.ucToGoSVR = 0x00;
            hdIQ.ucSrvType = (byte)'H';
            hdIQ.ucICMSect = (byte)'-';

            int szICHD_IQ = Marshal.SizeOf(typeof(ICHD_IQ));
            int szICHD_RTSEND = Marshal.SizeOf(typeof(ICHD_RTSEND));
            int szICHD_RT_FRM = Marshal.SizeOf(typeof(ICHD_RT_FRM));
            int ndLen = szICHD_RTSEND + (szICHD_RT_FRM * nCodeCount);

            hdIQ.ndLen = IPAddress.HostToNetworkOrder(ndLen);

            byte[] ucTrCode = Encoding.ASCII.GetBytes("0000");
            Buffer.BlockCopy(ucTrCode, 0, hdIQ.ucTrCode, 0, 4);

            if (bRegist)
            {
                hdRT.ucGubun = (byte)'1';
            }
            else
            {
                hdRT.ucGubun = (byte)'2';
            }

            hdRT.ucRExchFlg = (byte)'Z';
            hdRT.wTCnt = IPAddress.HostToNetworkOrder(nCodeCount);

            int nTotalSize = szICHD_IQ + szICHD_RTSEND + (szICHD_RT_FRM * nCodeCount);

            //전체크기 복사
            MemoryStream ms = new MemoryStream(nTotalSize + 4);
            //BinaryWriter bw = new BinaryWriter(ms);
            int nSwapTotalSize = IPAddress.HostToNetworkOrder(nTotalSize);
            ms.Write(BitConverter.GetBytes(nSwapTotalSize), 0, 4);

            //ICHD_IQ 복사
            byte[] buffHDIQ;
            StructToBytes(hdIQ, out buffHDIQ);
            ms.Write(buffHDIQ, 0, buffHDIQ.Length);

            //ICHD_RTSEND 복사
            byte[] buffRTSEND;
            StructToBytes(hdRT, out buffRTSEND);
            ms.Write(buffRTSEND, 0, buffRTSEND.Length);

            hdFM.ucCode = new byte[16];
            for (int i = 0; i < requestCodes.Count; i++)
            {
                hdFM.ucMarketFlag = ucMarketFlag;   //업종지수:'3' 뉴스:'B' 공시:'D'
                hdFM.ucClss = ucClass;              //REAL_CONTRACT 0x80
                string strReqCode = requestCodes[i].PadRight(16, ' ');
                byte[] bufReqCode = Encoding.ASCII.GetBytes(strReqCode);
                Array.Copy(bufReqCode, hdFM.ucCode, hdFM.ucCode.Length);
                //hdFM.ucCode = Encoding.ASCII.GetBytes(strReqCode);
                //ICHD_RT_FRM 복사
                byte[] buffRTFM;
                StructToBytes(hdFM, out buffRTFM);
                ms.Write(buffRTFM, 0, buffRTFM.Length);
            }

            byte[] buffer = ms.GetBuffer();

            //동기 전송
            //m_socket.Send(buffer);

            //비동기 전송
            SendData(buffer);
            /*
            if (async_send != null && m_socket != null)
            {
                async_send.SetBuffer(buffer, 0, buffer.Length);
                bool willRaiseEvent = m_socket.SendAsync(async_send);
                if (!willRaiseEvent)
                {
                    ProcessSend(async_send);
                }
                else
                {
                    if (sockType == RtfSockType.push)
                    {
                        Debug.WriteLine("리얼서버요청 eventSend.WaitOne ... 대기시작");
                        eventSend.WaitOne();
                        eventSend.Reset();
                        Debug.WriteLine("리얼서버요청 eventSend.WaitOne ... 대기종료");
                    }
                }
            }
            */
        }

        public static void StructToBytes(object obj, out byte[] packet)
        {
            int size = Marshal.SizeOf(obj);
            packet = new byte[size];
            IntPtr buffer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, buffer, false);
            Marshal.Copy(buffer, packet, 0, size);
            Marshal.FreeHGlobal(buffer);
        }

        public static void BytesToStructure(byte[] bValue, int start_idx, out object obj, Type t)
        {
            int size = Marshal.SizeOf(t);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            Marshal.Copy(bValue, start_idx, buffer, size);
            obj = Marshal.PtrToStructure(buffer, t);
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ST_HEADER
    {
        public Int32 DataLen;
        public Int32 TrCode;
        public Int32 Dest_Way;
        public Int32 Client_Handle;
        public Int32 User_Field;
        public Int32 Data_Type;
        public Int32 Client_Rtn1;
        public Int32 Client_Rtn2;
        public Int32 Client_Rtn3;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] User_ID;//[8];
        public byte CompressFlg;
        public byte KillGbn;
        public byte Filler1;
        public byte Filler2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Msg_cd;//[4];
        public Int32 Next_KeyLen;
        public Int32 Option_len;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ICHD_IQ
    {
        public byte ucCPflag;       // COMPRESSION, CIPHER, SUCCESSION
        public byte ucPacketID;       // PACKET ID
        public byte ucErrComm;       // ERROR INDICATOR '0': OK   '1': ERROR
        public byte ucToGoSVR;       // SERVER DESTINATION
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] ucTrCode;//[8]  // TR 코드
        public byte ucAbleFlg;       // PREV/NEXT INDICATOR
        public byte ucSrvType;      // 'H' : HTS          'C' : CTI
        public byte ucICMSect;       // 원장 작업구분이 들어간다.
        public byte uczpType;       //  COMPRESSION TYPE
        public byte ucSysType;       // 'I' : INTEL CHIP   'M' : MOTOROLA CHIP
        public byte ucTlog;       // ACCOUNT LOGGING TYPE
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] ucBranchNo;//[3]  // BRANCH NUMBER
        public byte ucMngb;     // INTER SERVER COMMAND TYPE
        public byte ucErgb;       // '0' : 2초 메시지 처리
        public byte ucHEflag;       // LANGUAGE TYPE '1' : KOREAN   '2' : ENGILISH
        public byte ucSvrSeq;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] ucNextKey;//[4]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] ucfiller;//[3]  // filler
        public int nClientNo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] ucMsgCode;//[4]
        public int nReqNo;
        public int hSCRID;
        public int hWinID;
        public int nSeqNum;
        public int nOrgLen;
        public int ndLen;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ICHD_RTSEND
    {
        public byte ucGubun;
        public byte ucRExchFlg;
        public short wTCnt;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ICHD_RT_FRM
    {
        public byte ucMarketFlag;
        public byte ucClss;
        public byte ucFiller0;
        public byte ucFiller1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucCode;//[16]
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ICHD_RT
    {
        public byte ucCPflag;
        public byte ucPacketID;
        public byte ucErrComm;
        public byte ucToGoSVR;
        public int nDLen;
        public int nDCnt;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ICHD_RT_CODE
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] ucCode;//[12]
        public int nRepCnt;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ST_INDEX
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] szFrameID;//[5];		//name="Frame ID    " type="string" size="5" Attr="Normal"	
        public byte chFiller1;          //name="Filler      " type="string" size="1" Attr="Normal"	
        public byte chJangGb;			//name="장구분      " type="string" size="1" Attr="Normal"	
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] szCode;//[3];			//name="지수업종코드" type="string" size="3" Attr="Normal"	
        public int nTime;               //name="시각        " type="int"    size="4" Attr="AtTime"	
        public int nClose;          //name="지수        " type="int"    size="4" Attr="AtFloat.2" 
        public byte chChangeGiho;       //name="등락폭부호  " type="string" size="1" Attr="Normal"	
        public int nChange;         //name="등락폭      " type="int"    size="4" Attr="Normal"	
        public byte chChgRateGiho;      //name="등락률 부호 " type="string" size="1" Attr="Normal"	
        public int nChgRate;            //name="등락률      " type="int"    size="4" Attr="AtFloat.2" 
        public int nOpen;               //name="시가지수    " type="int"    size="4" Attr="AtFloat.2" 
        public int nHigh;               //name="고가지수    " type="int"    size="4" Attr="AtFloat.2" 
        public int nLow;                //name="저가지수    " type="int"    size="4" Attr="AtFloat.2" 
        public long n64TickVol;     //name="순간거래량  " type="double" size="8" Attr="Normal"	
        public long n64Volume;          //name="거래량      " type="double" size="8" Attr="Normal"	
        public long n64Amount;		    //name="거래대금    " type="double" size="8" Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] szFiller2;//[3];		//
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ST_NEWS
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] szFrameID;//[5];		//name="Frame ID     "  type="string"   size="5"   Attr="Normal"
        public byte chFiller1;			//name="Filler       "  type="string"   size="1"   Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public byte[] szNewsNo;//[14];		//name="뉴스접수번호 "  type="string"   size="14"  Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] szFiller1;//[5];		//
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] szCode;//[12];		//name="종목코드     "  type="string"   size="12"  Attr="AtTime"
        public int nDate;				//name="일자         "  type="int"      size="4"   Attr="AtDate"
        public int nTime;               //name="시간         "  type="int"      size="4"   Attr="AtTime"
        public byte chMrkGb;            //name="시장구분     "  type="string"   size="1"   Attr="Normal"
        public byte chNewsGb;           //name="뉴스구분 코드"  type="string"   size="1"   Attr="Normal"
        public byte chLangGb;			//name="국문영문코드 "  type="string"   size="1"   Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 315)]
        public byte[] szTitle;//[315];		//name="뉴스제목     "  type="string"   size="315" Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] szFiller2;//[3];		//
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public struct ST_NOTICE
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] szFrameID;//[5];		//name="Frame ID"      type="string"   size="5"   Attr="Normal"
        public byte chFiller1;			//name="Filler"        type="string"   size="1"   Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public byte[] szNewsNo;//[14];		//name="공시접수번호"  type="string"   size="14"  Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] szCode;//[12];		//name="종목코드"      type="string"   size="12"  Attr="Normal"
        public int nDate;				//name="공시일자"      type="int"      size="4"   Attr="AtDate"
        public int nTime;				//name="공시시각"      type="int"      size="4"   Attr="AtTime"
        public byte chMrkGb;			//name="시장구분"      type="string"   size="1"   Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] szNoticeGb;//[5];		//name="공시사유코드"  type="string"   size="5"   Attr="Normal"
        public byte chLangGb;			//name="국문영문코드"  type="string"   size="1"   Attr="Normal"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 305)]
        public byte[] szTitle;//[305];//315 //name="공시제목"      type="string"   size="315" Attr="Normal"
        public byte szFiller1;		//
    }

    public delegate void SessionEventHandler(object sender, SessionEventArgs e);

    //세션이벤트
    public class SessionEventArgs : EventArgs
    {
        private static byte[] EmptyBuffer = new byte[0];
        public bool Connected = false;
        public DateTime _rcv_time;
        private byte[] _buffer = null;
        public byte[] Buffer
        {
            get { return _buffer != null ? _buffer : EmptyBuffer; }
        }
        public int Length
        {
            get { return Buffer.Length; }
        }

        public SessionEventArgs()
        {
        }

        public SessionEventArgs(bool bConnected)
        {
            this.Connected = bConnected;
        }

        public SessionEventArgs(byte[] buffer)
        {
            this._buffer = buffer;
        }

        private CommRcvInfo _rcv_info = null;
        public SessionEventArgs(CommRcvInfo rcv_info, DateTime rcv_time)
        {
            this._rcv_info = rcv_info;
            this._rcv_time = rcv_time;
        }

        public CommRcvInfo GetReceiveInfo()
        {
            return _rcv_info;
        }

        //////////////////////////////////////////////////////////////////////////
        //미사용
        //////////////////////////////////////////////////////////////////////////
        /*
        public SocketAsyncOperation LastOperation = SocketAsyncOperation.None;
        public SocketError SocketError = SocketError.TypeNotFound;
        public static Encoding _encoding = Encoding.Default;
        public string GetString() { return GetString(_encoding); }
        public string GetUnicodeString() { return GetString(Encoding.Unicode); }
        public string GetUtf8String() { return GetString(Encoding.UTF8); }
        public string GetString(Encoding encoding)
        {
            string data = string.Empty;
            if (_buffer != null)
            {
                try
                {
                    data = encoding.GetString(_buffer);
                }
                catch (Exception)
                {
                    data = "encoding error";
                }
            }
            return data;
        }
        */
    }
}
