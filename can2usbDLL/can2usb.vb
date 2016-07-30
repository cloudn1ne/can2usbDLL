'*******************************************************************************
'* can2usb dll for arduino/genuino + sparkfun/seedstudio shield or raspberry pi + pican2
'* version 1.0
'* (c) Georg Swoboda 2016 <cn@warp.at>
'*******************************************************************************
Imports System
Imports System.Threading
Imports System.IO.Ports
Imports System.Text

'Delete after debug
Imports System.IO

'#Const DBG_RX_IN = 1
'#Const DBG_RX = 1
'#Const DBG_RX_VALID = 1
'#Const DBG_RX_ERR4 = 1
'#Const DBG_RX_COPY = 1
'#Const DBG_ID_TRIGGER = 1
'#Const DBG_ID_TRIGGER_TO = 1

Public Class can2usb

    Private fs As FileStream
    Private sw As StreamWriter

    Private ComPort As SerialPort
    Private WithEvents tcpClient As AsyncSocket
    Private UsingSerial As Boolean = True
    Private is_open As Boolean = False

    '   Public Shared recv_buffer As String = ""
    Private buf() As Byte = New Byte() {}
    Private START_PATTERN() As Byte = System.Text.Encoding.ASCII.GetBytes("$F")

    ' Triggering
    Private TriggerEvent = New EventWaitHandle(False, EventResetMode.AutoReset)

    ' Default timeout for shield used
    Private ShieldTimeout As Integer = 100

    ' Statistics
    'Private StatInterlock As New Object
    Private StatRXValidReceived As Integer = 0
    Private StatRXCRCErrors As Integer = 0
    Private StatMessagesSent As Integer = 0
    Private StatRXShortMsgErrors As Integer = 0
    Private StatRXWaitForIDTimeouts As Integer = 0

    ' CAN Message handling
    Public ReadOnly MaxCANMessageBufferSize As Integer = 64
    Private CANMessagesIdx As Integer = 0
    Private Shared CANMessages(64) As CANMessage
    Private CANMessagesOverrun As Boolean = True
    'Private CANMessageIDTriggerInterlock As New Object
    Private Shared CANMessageIDTriggerID As Integer
    Private Shared CANMessageIDTriggerCounter As Integer
    Private Shared CANMessageIDTriggerFlag As Boolean = False

    ' CAN shield types
    Public Enum ShieldType As Integer
        SparkFun = 1
        SeedStudio = 2
        PiCAN2 = 3
    End Enum

    ' CAN message structure
    Structure CANMessage
        Dim id As Integer
        Dim len As Integer
        Dim data() As Byte
        Dim used As Boolean
    End Structure

    '*****************************************************
    '* Return number of bytes in buf()
    '*****************************************************
    Public Function GetByteBufferSize() As Integer
        Return (buf.Length)
    End Function


    '*****************************************************
    '* Return connection status
    '*****************************************************
    Public Function isConnected() As Boolean
        Return (is_open)
    End Function

    '*****************************************************
    '* Connect to USB_CAN adapter
    '*****************************************************
    Public Function Connect(ByVal COMPortName As String, ByVal ResetArduino As Boolean, ByVal shield As Integer) As Boolean
        If (COMPortName Is Nothing) Then
            Return (False)
        End If
        If (Me.is_open) Then
            Me.Disconnect()
        End If
        Try
            If (shield = ShieldType.PiCAN2) Then
                UsingSerial = False
                ShieldTimeout = 200
                tcpClient = New AsyncSocket
                ' Next 3 lines for possible later implementation, if needed
                'tcpClient.NoDelay = True
                'tcpClient.ReceiveTimeout = ShieldTimeout
                'tcpClient.SendTimeout = ShieldTimeout
                tcpClient.Connect("192.168.1.157", 8069)
            Else
                ComPort = New SerialPort
                With ComPort
                    .PortName = COMPortName
                    .BaudRate = 115200
                    .Parity = IO.Ports.Parity.None
                    .DataBits = 8
                    .StopBits = IO.Ports.StopBits.One
                    .Handshake = IO.Ports.Handshake.XOnXOff
                    If ResetArduino Then
                        .DtrEnable = True
                    Else
                        .DtrEnable = False
                    End If
                    .Encoding = System.Text.Encoding.ASCII
                    .NewLine = vbCrLf
                    .ReadTimeout = ShieldTimeout
                    .ReadBufferSize = 1000
                    .ReceivedBytesThreshold = 1   'threshold: one byte in buffer > event is fired                           
                End With
                AddHandler ComPort.DataReceived, New SerialDataReceivedEventHandler(AddressOf ComPort_DataReceived)
                ComPort.Open()
                If ResetArduino Then
                    Thread.Sleep(2000)
                End If
            End If
            Me.is_open = True
        Catch ex As Exception
            Return (False)
        End Try
        Return (True)
    End Function

    '*****************************************************
    '* Close COMPort
    '*****************************************************
    Public Sub Disconnect()
        If (Me.is_open) Then
            is_open = False
            If UsingSerial Then
                ComPort.Close()
            Else
                tcpClient.Close()
            End If
        End If
    End Sub

    '*****************************************************
    '* Reset CANMessages buffer handling 
    '*****************************************************
    Public Sub ResetCANMessages()
        SyncLock (CANMessages)
            For i As Integer = 0 To CANMessages.Length - 1
                CANMessages(i).used = False
                CANMessages(i).id = 0
            Next
        End SyncLock
        Interlocked.Exchange(CANMessagesIdx, 0)
        Interlocked.Exchange(CANMessagesOverrun, False)
    End Sub

    '*****************************************************
    '* Add one CANMessage to CANMessages array
    '*****************************************************
    Private Sub AddCANMessage(ByRef cmsg As CANMessage)
        'Dim oldsize As Integer = 0

        'Console.WriteLine("AddCANMessage()")
        SyncLock (CANMessages)
            CANMessages(Interlocked.Read(CANMessagesIdx)) = cmsg
            If (Interlocked.Read(CANMessagesIdx) < CANMessages.Length - 2) Then
                Interlocked.Increment(CANMessagesIdx)
            Else
                ' indicate overun of buffer                
                Interlocked.Exchange(CANMessagesOverrun, True)
            End If
            'Console.WriteLine("AddCANMessage() done" & CANMessagesIdx & " " & CANMessages.Length)
        End SyncLock

    End Sub

    '*****************************************************
    '* Get copy of CANMessages array
    '*****************************************************
    Public Function GetCANMessagesBuffer() As CANMessage()
        Dim CANMessages_copy() As CANMessage = Nothing
        Dim idx = Interlocked.Read(CANMessagesIdx)

        Array.Resize(CANMessages_copy, idx)
        SyncLock (CANMessages)
            Array.Copy(CANMessages, CANMessages_copy, idx)
        End SyncLock
        Return (CANMessages_copy)
    End Function

    '*****************************************************
    '* Get first CANMessage buffer matching can_id
    '* if not found, Nothing is returned
    '*****************************************************
    Public Function GetFirstCANMessageBufferByID(ByVal can_id As Integer) As CANMessage
        Dim CANMessages_copy As New CANMessage

        ' indicate that we dont have a valid result yet
        CANMessages_copy.used = 0

        For i As Integer = 0 To Interlocked.Read(CANMessagesIdx)
            SyncLock (CANMessages)
                If (CANMessages(i).id = can_id) Then
                    CANMessages_copy.id = CANMessages(i).id
                    CANMessages_copy.len = CANMessages(i).len
                    Array.Resize(CANMessages_copy.data, CANMessages(i).len)
                    Array.Copy(CANMessages(i).data, CANMessages_copy.data, CANMessages(i).len)
                    CANMessages_copy.used = 1
                    Exit For
                End If
            End SyncLock
        Next

        Return (CANMessages_copy)
    End Function

    '*****************************************************
    '* Get last CANMessage buffer matching can_id
    '* if not found, Nothing is returned
    '*****************************************************
    Public Function GetLastCANMessageBufferByID(ByVal can_id As Integer) As CANMessage
        Dim CANMessages_copy As New CANMessage

        ' indicate that we dont have a valid result yet
        CANMessages_copy.used = 0

        For i As Integer = 0 To Interlocked.Read(CANMessagesIdx)
            SyncLock (CANMessages)
                If (CANMessages(i).id = can_id) Then
                    CANMessages_copy.id = CANMessages(i).id
                    CANMessages_copy.len = CANMessages(i).len
                    Array.Resize(CANMessages_copy.data, CANMessages(i).len)
                    Array.Copy(CANMessages(i).data, CANMessages_copy.data, CANMessages(i).len)
                    CANMessages_copy.used = 1
                End If
            End SyncLock
        Next
        Return (CANMessages_copy)
    End Function

    '*****************************************************
    '* return bool status if we overrun the CANMessages array 
    '*****************************************************
    Public Function GetCANMessagesOverrun() As Boolean
        Dim state As Boolean
        state = Interlocked.Read(CANMessagesOverrun)
        Return (state)
    End Function

    Public Function GetCANMessagesIdx() As Integer
        Dim r As Integer
        r = GetCANMessagesBuffersUsed()
        Return (r)
    End Function

    '*****************************************************
    '* return number of used buffers in CANMessages array
    '*****************************************************
    Public Function GetCANMessagesBuffersUsed() As Integer
        Dim count As Integer = 0
        count = Interlocked.Read(CANMessagesIdx)
        Return (count)
    End Function

    '*****************************************************
    '* set the CAN Message ID Trigger ID 
    '* and reset the Counter
    '*****************************************************
    Private Sub SetCANMessageIDTriggerID(ByVal TriggerID As Integer)
        Interlocked.Exchange(CANMessageIDTriggerID, TriggerID)
    End Sub


    '*****************************************************
    '* return the CAN Message ID Trigger flag
    '*****************************************************
    Public Function GetCANMessageIDTrigger() As Boolean
        Dim b As Boolean
        b = Interlocked.Read(CANMessageIDTriggerFlag)
        Return (b)
    End Function

    '*****************************************************
    '* return the CAN Message ID Trigger counter
    '*****************************************************
    Public Function GetCANMessageIDTriggerCounter() As Boolean
        Dim c As Integer
        c = Interlocked.Read(CANMessageIDTriggerCounter)
        Return (c)
    End Function

    '*****************************************************
    '* reset the CAN Message ID Trigger flag
    '*****************************************************
    Private Sub ResetCANMessageIDTrigger()
        Interlocked.Exchange(CANMessageIDTriggerFlag, False)
        Interlocked.Exchange(CANMessageIDTriggerCounter, 0)
    End Sub

    '******************************************************************
    '* Wait for CAN Message ID Trigger ID being in the buffer
    '* returns true if triggered within timeout (ShieldTimeout msec)
    '* returns false if timeout happend while waiting for the right msg
    '******************************************************************
    Private Function WaitForCANMessageIDTrigger() As Boolean
        Dim sw As New Stopwatch
        Dim b As Boolean

        sw.Reset()
        sw.Start()
        While (Interlocked.Read(CANMessageIDTriggerFlag) = False)
            TriggerEvent.WaitOne(New TimeSpan(0, 0, 1))
            If (sw.ElapsedMilliseconds > ShieldTimeout) Then
#If DBG_ID_TRIGGER_TO Then
                Console.WriteLine("WaitForCANMessageIDTrigger(0x" & Hex(CANMessageIDTriggerID) & ") - timeout")
#End If
                Interlocked.Increment(StatRXWaitForIDTimeouts)
                Return (False)
            End If
        End While
        Return (True)
    End Function

    '****************************************************************
    '* Wait for CAN Message ID Trigger ID being in the buffer
    '* returns true if triggered within timeout (Timeout)
    '* returns false if timeout happend while waiting for the right msg
    '****************************************************************
    Private Function WaitForCANMessageIDTrigger(ByVal Timeout As Integer) As Boolean
        Dim sw As New Stopwatch

        sw.Reset()
        sw.Start()
        While (Interlocked.Read(CANMessageIDTriggerFlag) = False)
            TriggerEvent.WaitOne(New TimeSpan(0, 0, 1))
            If (sw.ElapsedMilliseconds > Timeout) Then
#If DBG_ID_TRIGGER_TO Then
                Console.WriteLine("WaitForCANMessageIDTrigger(0x" & Hex(CANMessageIDTriggerID) & ") - timeout")
#End If
                Interlocked.Increment(StatRXWaitForIDTimeouts)
                Return (False)
            End If
        End While
        Return (True)
    End Function


    '**************************************************************************
    '* Wait for 'num' messages of CAN Message ID Trigger ID being in the buffer
    '* returns true if num messages expected arrived
    '* returns false if timeout happend while waiting for message num
    '**************************************************************************
    Private Function WaitForNumOfCANMessageIDTriggers(ByVal num As Integer) As Boolean
        Dim sw As New Stopwatch
        Dim b As Boolean
        Dim c As Integer = ShieldTimeout

        If ShieldTimeout > 100 Then
            c = num * 50
        End If
        sw.Reset()
        sw.Start()
        While (Interlocked.Read(CANMessageIDTriggerCounter) < num)
            If (sw.ElapsedMilliseconds > c) Then
#If DBG_ID_TRIGGER_TO Then
                'Console.WriteLine("WaitForNumOfCANMessageIDTriggers(0x" & Hex(CANMessageIDTriggerID) & ") - timeout")
                'Console.WriteLine(c & "/" & num)
                'Console.WriteLine("TIMEOUT")
#End If
                Interlocked.Increment(StatRXWaitForIDTimeouts)
                Return (False)
            End If
            TriggerEvent.WaitOne(New TimeSpan(0, 0, 0, 0, 1))
        End While
        Return (True)
    End Function

    '*****************************************************
    '* Called when new data arrives on the SerialPort
    '* analyze serial data for valid messages
    '* extract them and put into the CANMessages() buffer
    '*****************************************************
    Private Sub ComPort_DataReceived(ByVal sender As Object, ByVal evt As SerialDataReceivedEventArgs)
        Dim msg_str As String
        Dim s As Integer
        Dim copy_start_idx As Integer
        Dim cmsg As CANMessage

        If ComPort.IsOpen Then
            Try
                Dim buf_len As Integer = buf.Length

                Dim ser_len As Integer

                ser_len = ComPort.BytesToRead

#If DBG_RX_IN Then
                If (buf_len > 0) Then
                    msg_str = ""
                    For i = 0 To buf.Length - 1
                        msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                    Next
                    Console.WriteLine("existing_str(" & buf.Length & "): " & msg_str & "")
                End If
                Console.WriteLine("received_bytes(" & ser_len & ")")
#End If
                ' make space for new data in buf()
                Array.Resize(buf, buf_len + ser_len)
                ' append data from ComPort to buf()

                ComPort.Read(buf, buf_len, ser_len)

#If DBG_RX_IN Then
                msg_str = ""
                For i = 0 To buf.Length - 1
                    msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                Next
                Console.WriteLine("received_str(" & buf.Length & "): " & msg_str & "")
#End If
                ' try to extract CANMessages from buf
                ' find '$F' marker                
                Dim count As Integer = 0

                copy_start_idx = 0
                s = 0
                s = SearchBytePattern(START_PATTERN, buf, s)
                While (s > -1)
                    cmsg = ExtactCANMessage(buf, s)
                    If (cmsg.id >= 0) Then
                        Interlocked.Increment(StatRXValidReceived)
#If DBG_RX_VALID Then
                        msg_str = ""
                        For i = s To s + cmsg.len + 6 ' buf.Length - 1
                            msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                        Next
                        Console.WriteLine("valid_msg(0x" & Hex(cmsg.id) & "/" & s & ") " & msg_str)
#End If
                        ' message decoded fine, store for later use                        
                        AddCANMessage(cmsg)
                        ' probe trigger id and set flag if matched
                        'SyncLock (CANMessageIDTriggerInterlock)
                        If (cmsg.id = Interlocked.Read(CANMessageIDTriggerID)) Then
                            Interlocked.Exchange(CANMessageIDTriggerFlag, True)
                            Interlocked.Increment(CANMessageIDTriggerCounter)
                            TriggerEvent.Set()
#If DBG_ID_TRIGGER Then
                            Console.WriteLine("CANMessageIDTriggerID() Matched")
#End If
                        End If
                        'End SyncLock
                        count = count + 1
                        'SyncLock (recv_buffer)
                        ' recv_buffer &= CANMessageToString(cmsg)
                        ' count = count + 1
                        ' End SyncLock                        
                        copy_start_idx = s + cmsg.len + 7
                    Else
                        'Console.WriteLine("invalid message hit " & cmsg.id)
                        ' something wrong with that message so save for next round of processing
                        If (cmsg.id < 0) Then
                            ' CRC Error
                            If (cmsg.id = -4) Then
                                Interlocked.Increment(StatRXCRCErrors)
                            End If
                            ' Short message error
                            If (cmsg.id = -3) Then
                                Interlocked.Increment(StatRXShortMsgErrors)
                            End If
                            'If (cmsg.id = -2) Then
                            'Console.WriteLine("-2 status")
                            'End If
                            '   If (cmsg.id = -1) Then
                            'Console.WriteLine("-1 status")
                            'End If
#If DBG_RX_ERR4 Then
                            msg_str = ""
                            For i = s To buf.Length - 1 ' s + 14
                                msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                            Next
                            Console.WriteLine("invalid_msg(" & cmsg.id & "/" & s & "/" & buf.Length & ") " & msg_str)
#End If
                        End If
                        copy_start_idx = s
                        '    Exit While
                    End If
                    ' find the next pattern
                    s = SearchBytePattern(START_PATTERN, buf, s + START_PATTERN.Length)
                End While

                'Console.WriteLine("extracted " & count & " messages")

                Dim copy_len = buf.Length - copy_start_idx
#If DBG_RX_COPY Then
                Console.WriteLine("copy_len: " & copy_len)
                Console.WriteLine("copy_start_idx: " & copy_start_idx)
                Console.WriteLine("buf.length(): " & buf.Length)
#End If
                If (copy_len > 0) Then
#If DBG_RX_COPY Then
                    msg_str = ""
                    For i = copy_start_idx To copy_start_idx + copy_len - 1
                        msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                    Next
                    Console.WriteLine("copy_str: " & msg_str & "")
#End If
                    Array.Copy(buf, copy_start_idx, buf, 0, copy_len)
                End If
                Array.Resize(buf, copy_len)
            Catch ex As Exception
                Exit Sub
            End Try
        End If
    End Sub
    '*****************************************************
    '* Called when new data arrives on the TCP socket
    '* analyze serial data for valid messages
    '* extract them and put into the CANMessages() buffer
    '*****************************************************
    Private Sub tcpClient_DataReceived(ByVal SocketID As String, ByVal tmp_buf As Byte(), ByVal ser_len As Integer) Handles tcpClient.socketDataArrival
        Dim msg_str As String
        Dim s As Integer
        Dim copy_start_idx As Integer
        Dim cmsg As CANMessage

        If tcpClient.IsOpen Then
            Try
                Dim buf_len As Integer = buf.Length

                'Dim ser_len As Integer
                'ser_len = tmp_buf.Length
#If DBG_RX_IN Then
                If (buf_len > 0) Then
                    msg_str = ""
                    For i = 0 To buf.Length - 1
                        msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                    Next
                    Console.WriteLine("existing_str(" & buf.Length & "): " & msg_str & "")
                End If
                Console.WriteLine("received_bytes(" & ser_len & ")")
#End If
                ' make space for new data in buf()
                Array.Resize(buf, buf_len + ser_len)
                ' append data from tcpClient to buf()
                Array.Copy(tmp_buf, 0, buf, buf_len, ser_len)
#If DBG_RX_IN Then
                msg_str = ""
                For i = 0 To buf.Length - 1
                    msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                Next
                Console.WriteLine("received_str(" & buf.Length & "): " & msg_str & "")
#End If
                ' try to extract CANMessages from buf
                ' find '$F' marker                
                Dim count As Integer = 0

                copy_start_idx = 0
                s = 0
                s = SearchBytePattern(START_PATTERN, buf, s)
                While (s > -1)
                    cmsg = ExtactCANMessage(buf, s)
                    If (cmsg.id >= 0) Then
                        Interlocked.Increment(StatRXValidReceived)
#If DBG_RX_VALID Then
                        msg_str = ""
                        For i = s To s + cmsg.len + 6 ' buf.Length - 1
                            msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                        Next
                        Console.WriteLine("valid_msg(0x" & Hex(cmsg.id) & "/" & s & ") " & msg_str)
#End If
                        ' message decoded fine, store for later use                        
                        AddCANMessage(cmsg)
                        ' probe trigger id and set flag if matched
                        'SyncLock (CANMessageIDTriggerInterlock)
                        If (cmsg.id = Interlocked.Read(CANMessageIDTriggerID)) Then
                            Interlocked.Exchange(CANMessageIDTriggerFlag, True)
                            Interlocked.Increment(CANMessageIDTriggerCounter)
                            TriggerEvent.Set()
#If DBG_ID_TRIGGER Then
                            Console.WriteLine("CANMessageIDTriggerID() Matched")
#End If
                        End If
                        'End SyncLock
                        count = count + 1
                        'SyncLock (recv_buffer)
                        ' recv_buffer &= CANMessageToString(cmsg)
                        ' count = count + 1
                        ' End SyncLock                        
                        copy_start_idx = s + cmsg.len + 7
                    Else
                        'Console.WriteLine("invalid message hit " & cmsg.id)
                        ' something wrong with that message so save for next round of processing
                        If (cmsg.id < 0) Then
                            ' CRC Error
                            If (cmsg.id = -4) Then
                                Interlocked.Increment(StatRXCRCErrors)
                            End If
                            ' Short message error
                            If (cmsg.id = -3) Then
                                Interlocked.Increment(StatRXShortMsgErrors)
                            End If
                            'If (cmsg.id = -2) Then
                            'Console.WriteLine("-2 status")
                            'End If
                            '   If (cmsg.id = -1) Then
                            'Console.WriteLine("-1 status")
                            'End If
#If DBG_RX_ERR4 Then
                            msg_str = ""
                            For i = s To buf.Length - 1 ' s + 14
                                msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                            Next
                            Console.WriteLine("invalid_msg(" & cmsg.id & "/" & s & "/" & buf.Length & ") " & msg_str)
#End If
                        End If
                        copy_start_idx = s
                        '    Exit While
                    End If
                    ' find the next pattern
                    s = SearchBytePattern(START_PATTERN, buf, s + START_PATTERN.Length)
                End While

                'Console.WriteLine("extracted " & count & " messages")

                Dim copy_len = buf.Length - copy_start_idx
#If DBG_RX_COPY Then
                Console.WriteLine("copy_len: " & copy_len)
                Console.WriteLine("copy_start_idx: " & copy_start_idx)
                Console.WriteLine("buf.length(): " & buf.Length)
#End If
                If (copy_len > 0) Then
#If DBG_RX_COPY Then
                    msg_str = ""
                    For i = copy_start_idx To copy_start_idx + copy_len - 1
                        msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                    Next
                    Console.WriteLine("copy_str: " & msg_str & "")
#End If
                    Array.Copy(buf, copy_start_idx, buf, 0, copy_len)
                End If
                Array.Resize(buf, copy_len)
            Catch ex As Exception
                Exit Sub
            End Try
        End If
    End Sub

    Private Sub tcpClient_socketDisconnected(ByVal SocketID As String) Handles tcpClient.socketDisconnected
        tcpClient.IsOpen = False
        Me.Disconnect()
        'Console.WriteLine("Socket closed properly.")
    End Sub
    '*****************************************************
    '* Send CAN request (no waiting)
    '*****************************************************
    Public Function SendCANMessage(ByVal Request As CANMessage) As Boolean
        Dim r As Boolean = False

        If (is_open) Then
            If (UsingSerial AndAlso ComPort.IsOpen) Or (Not UsingSerial AndAlso tcpClient.IsOpen) Then
                Dim query As String = "$S," & Hex(Request.len) & "," & Hex(Request.id)
                For i As Integer = 0 To Request.len - 1
                    query &= "," & Hex(Request.data(i))
                Next
                query &= vbCrLf
                If UsingSerial Then
                    ComPort.Write(query)
                Else
                    tcpClient.Send(query)
                End If
                'Console.WriteLine("SendCANMessage() = " & query)
                r = True
            End If
        End If
        Return (r)
    End Function

    '*****************************************************
    '* Send CAN request and wait for reply (blocking)
    '*****************************************************
    Public Function SendAndWaitForCANMessageID(ByVal Request As CANMessage, ByVal CANTriggerID As Integer) As Boolean
        Dim r As Boolean = False

        If (is_open) Then
            If (UsingSerial AndAlso ComPort.IsOpen) Or (Not UsingSerial AndAlso tcpClient.IsOpen) Then
                If (UsingSerial AndAlso ComPort.BytesToWrite = 0) Or Not UsingSerial Then
                    'ComPort.ReadExisting()
                    ResetCANMessages()
                    SetCANMessageIDTriggerID(CANTriggerID)
                    ResetCANMessageIDTrigger()
                    Dim query As String = "$S," & Hex(Request.len) & "," & Hex(Request.id)
                    For i As Integer = 0 To Request.len - 1
                        query &= "," & Hex(Request.data(i))
                    Next
                    query &= vbCrLf
                    If UsingSerial Then
                        ComPort.Write(query)
                    Else
                        tcpClient.Send(query)
                    End If
                    'Console.WriteLine("SendAndWaitForCANMessageID()" & query)
                    r = WaitForCANMessageIDTrigger()
                End If
            End If
        End If

        Return (r)
    End Function

    '*****************************************************
    '* Wait for CAN ID to be received (blocking)
    '*****************************************************
    Public Function WaitForCANMessageID(ByVal CANTriggerID As Integer, ByVal DoReset As Boolean) As Boolean
        Dim r As Boolean = False

        If (is_open) Then
            If (UsingSerial AndAlso ComPort.IsOpen) Or (Not UsingSerial AndAlso tcpClient.IsOpen) Then
                If (UsingSerial AndAlso ComPort.BytesToWrite = 0) Or Not UsingSerial Then
                    'ComPort.ReadExisting()
                    If (DoReset) Then
                        ResetCANMessages()
                        SetCANMessageIDTriggerID(CANTriggerID)
                        ResetCANMessageIDTrigger()
                    End If
                    'Console.WriteLine("WaitForCANMessageID()" & query)
                    r = WaitForCANMessageIDTrigger()
                End If
            End If
        End If
        Return (r)
    End Function

    '*****************************************************
    '* Send CAN request and wait for reply (blocking)
    '* Overload with Timeout parameter (in milliseconds)
    '*****************************************************
    Public Function SendAndWaitForCANMessageID(ByVal Request As CANMessage, ByVal CANTriggerID As Integer, ByVal Timeout As Integer) As Boolean
        Dim r As Boolean = False

        If (is_open) Then
            If (UsingSerial AndAlso ComPort.IsOpen) Or (Not UsingSerial AndAlso tcpClient.IsOpen) Then
                'ComPort.ReadExisting()
                ResetCANMessages()
                SetCANMessageIDTriggerID(CANTriggerID)
                ResetCANMessageIDTrigger()
                Dim query As String = "$S," & Hex(Request.len) & "," & Hex(Request.id)
                For i As Integer = 0 To Request.len - 1
                    query &= "," & Hex(Request.data(i))
                Next
                query &= vbCrLf
                If UsingSerial Then
                    ComPort.Write(query)
                Else
                    tcpClient.Send(query)
                End If
                'Console.WriteLine("SendAndWaitForCANMessageID() " & query)
                r = WaitForCANMessageIDTrigger(Timeout)
            End If
        End If
        Return (r)
    End Function

    '************************************************************************
    '* Send CAN request and wait for reply (blocking)
    '* this waits until 'num' messages from 'CANTriggerID' have been received
    '************************************************************************
    Public Function SendAndWaitForNumOfCANMessageIDs(ByVal Request As CANMessage, ByVal CANTriggerID As Integer, ByVal num As Integer) As Boolean
        Dim r As Boolean = False

        If (is_open) Then
            If (UsingSerial AndAlso ComPort.IsOpen) Or (Not UsingSerial AndAlso tcpClient.IsOpen) Then
                'ComPort.ReadExisting()
                ResetCANMessages()
                SetCANMessageIDTriggerID(CANTriggerID)
                ResetCANMessageIDTrigger()
                Dim query As String = "$S," & Hex(Request.len) & "," & Hex(Request.id)
                For i As Integer = 0 To Request.len - 1
                    query &= "," & Hex(Request.data(i))
                Next
                query &= vbCrLf
                If UsingSerial Then
                    ComPort.Write(query)
                Else
                    tcpClient.Send(query)
                End If
                r = WaitForNumOfCANMessageIDTriggers(num)
            End If
        End If
        Return (r)
    End Function

    '*****************************************************
    '* Extract received $F CAN message and store into
    '* CANMessage structure element
    '*****************************************************
    Private Function ExtactCANMessage(ByRef Bytes() As Byte, ByVal start As Integer) As CANMessage
        Dim cmsg As New CANMessage
        Dim i As Short
        Dim crc As Integer = 0

        cmsg.used = False
        cmsg.id = -1
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' CAN frame with no payload is 8 bytes, so this is the minimum length
        ' of Bytes() otherwise we are fooked
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        If (Bytes.Length < (start + 7)) Then
            Return (cmsg)
        End If
        Try
            '''''''''''''''''''''''''''''''''''''''''''''''''''
            ' Extract .len and prepare .data
            '''''''''''''''''''''''''''''''''''''''''''''''''''
            cmsg.len = Bytes(2 + start) / 16
            'Console.WriteLine("cmsg.len =" & cmsg.len)
            ' make sure overall message size is long enough now that we know how long the .data is
            If (Bytes.Length < (start + cmsg.len + 7)) Then
                cmsg.id = -2
                Return (cmsg)
            End If
            Array.Resize(cmsg.data, cmsg.len)
        Catch ex As Exception
            Dim str = "Start: " & start & vbCrLf & "Bytes length: " & Bytes.Length
            For i = start To Bytes.Length - 1
                str &= " " & Hex(Bytes(i)) & "{" & i & "}"
            Next
            MsgBox("Error in decoding CAN Frame - Extracting length" & vbCrLf & str)
        End Try

        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract .id
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        Try
            cmsg.id = (Bytes(2 + start) And &HF) * 256 Or Bytes(3 + start)
            crc += Bytes(2 + start)
            crc += Bytes(3 + start)
            'Console.WriteLine("cmsg.id = 0x" & Hex(cmsg.id))
        Catch ex As Exception
            Dim str = "Start: " & start & vbCrLf & "Bytes len: " & Bytes.Length & vbCrLf
            For i = start To Bytes.Length - 1
                str &= " " & Hex(Bytes(i)) & "{" & i & "}"
            Next
            MsgBox("Error in decoding CAN Frame - Extracting ID" & vbCrLf & str)
        End Try
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract .data
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        Try
            For i = 0 To cmsg.len - 1
                cmsg.data(i) = Bytes(4 + start + i)
                crc += Bytes(4 + start + i)
            Next
        Catch ex As Exception
            Dim str = "Start: " & start & vbCrLf & "Bytes len: " & Bytes.Length & vbCrLf & "CAN len:" & cmsg.len & vbCrLf & "i:" & i & vbCrLf
            For i = start To Bytes.Length - 1
                str &= " " & Hex(Bytes(i)) & "{" & i & "}"
            Next
            MsgBox("Error in decoding CAN Frame - Extracting Data" & vbCrLf & str)
        End Try
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Check location of CRC and its value
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        Try
            ' check if the message is long enough to have a CRC byte
            If (Bytes.Length < (4 + start + cmsg.len)) Then
                cmsg.id = -3
            Else
                ' check CRC
                If (Bytes(4 + cmsg.len + start) <> (crc And &HFF)) Then
                    ' Console.WriteLine("CRC invalid than expected " & Hex(crc And &HFF) & " expected " & Hex(Bytes(4 + cmsg.len + start)))
                    cmsg.id = -4
                End If
            End If
        Catch ex As Exception
            Dim str = "Start: " & start & vbCrLf & "Bytes len: " & Bytes.Length & vbCrLf & "CAN len:" & cmsg.len & vbCrLf
            For i = start To Bytes.Length - 1
                str &= " " & Hex(Bytes(i)) & "{" & i & "}"
            Next
            MsgBox("Error in decoding CAN Frame - checking CRC" & vbCrLf & str)
        End Try
        ' indicate successful extraction
        cmsg.used = True
        Return (cmsg)
    End Function

    '*****************************************************
    '* search for pattern in Bytes() starting from start 
    '* offset - used to identfy data blocks in $F messages   
    '*****************************************************
    Public Shared Function SearchBytePattern(ByRef pattern() As Byte, ByRef Bytes() As Byte, ByVal start As Integer) As Integer
        Dim matches As Integer = -1
        Dim i, j As Integer
        Dim ismatch As Boolean
        ' precomputing this shaves some seconds from the loop execution
        Dim maxloop As Integer = Bytes.Length - pattern.Length

        'Console.WriteLine("maxloop: " & maxloop)
        For i = start To maxloop - 1
            ' first byte in pattern matches in Bytes
            If (pattern(0) = Bytes(i)) Then
                ismatch = True
                For j = 1 To pattern.Length - 1
                    If (Bytes(i + j) <> pattern(j)) Then
                        ismatch = False
                        Exit For
                    End If
                Next j
                If (ismatch) Then
                    matches = i
                    Exit For
                End If
            End If
        Next i
        Return matches
    End Function

    '*******************************************************************
    '* return number of valid messages received (crc ok, formatting ok)
    '*******************************************************************
    Public Function GetRXValidReceived() As Integer
        Dim r As Integer = 0
        r = Interlocked.Read(StatRXValidReceived)
        Return (r)
    End Function

    '*******************************************************************
    '* return number of CRC errors encounted while parsing RX messages
    '*******************************************************************
    Public Function GetRXCRCErrors() As Integer
        Dim r As Integer = 0
        r = Interlocked.Read(StatRXCRCErrors)
        Return (r)
    End Function

    '*******************************************************************
    '* return number of messages sent
    '*******************************************************************
    Public Function GetMessagesSent() As Integer
        Dim r As Integer = 0
        r = Interlocked.Read(StatMessagesSent)
        Return (r)
    End Function

    '*******************************************************************
    '* return number of "short" messages
    '*******************************************************************
    Public Function GetRXShortMsgErrors() As Integer
        Dim r As Integer = 0
        r = Interlocked.Read(StatRXShortMsgErrors)
        Return (r)
    End Function

    '*******************************************************************
    '* return number timeouts that happend while waiting for CAN RX ID's
    '*******************************************************************
    Public Function GetRXWaitForIDTimeouts() As Integer
        Dim r As Integer = 0
        r = Interlocked.Read(StatRXWaitForIDTimeouts)
        Return (r)
    End Function


    '*****************************************************
    '* Return adapter version
    '*****************************************************
    Public Function GetVersion() As String
        Return ("can2usb-1.1")
    End Function

    '*****************************************************
    '* Initialize adapter
    '*****************************************************
    Public Function Init(ByVal speed As Integer, ByVal shield As Integer) As Boolean
        If (UsingSerial AndAlso ComPort.IsOpen) Or (Not UsingSerial AndAlso tcpClient.IsOpen) Then
            ' check for valid CAN Speeds        
            If ((speed = 125) Or (speed = 250) Or (speed = 500) Or (speed = 1000)) Then
                Dim query As String = ""
                query = "$I," & speed & "," & shield & vbCrLf
                If UsingSerial Then
                    ComPort.Write(query)
                Else
                    tcpClient.Send(query)
                End If
                Return (True)
            End If
        End If
        Return (False)
    End Function
End Class


