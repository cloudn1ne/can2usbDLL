'*********************************************************************************************
'* can2usb dll for arduino/genuino + sparkfun/seedstudio shield or raspberry pi + pican2
'* version 1.1
'* (c) Georg Swoboda 2016 <cn@warp.at>, Robert Baizer
'*********************************************************************************************
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
'#Const DBG_EXTRACT = 1
'#Const DBG_EXTRACT_SEARCH = 1
'#Const DBG_EXTRACT_CAN = 1
'#Const DBG_EXTRACT_KLINE = 1
#Const DBG_ERROR_HANDLER = 1

Public Class can2usb
    'Private fs As FileStream
    'Private sw As StreamWriter

    Private ComPort As SerialPort
    Private WithEvents tcpClient As AsyncSocket
    Private UsingSerial As Boolean = True
    Private is_open As Boolean = False

    '   Public Shared recv_buffer As String = ""
    Private buf() As Byte = New Byte() {}
    Private START_PATTERN_VER() As Byte = System.Text.Encoding.ASCII.GetBytes("$VER")
    Private START_PATTERN_CANRX() As Byte = System.Text.Encoding.ASCII.GetBytes("$F")
    Private START_PATTERN_KLINERX() As Byte = System.Text.Encoding.ASCII.GetBytes("$KO")
    Private START_PATTERN_KLINEINIT() As Byte = System.Text.Encoding.ASCII.GetBytes("$KSI,")
    Private START_PATTERN_ERROR() As Byte = System.Text.Encoding.ASCII.GetBytes("$E,")
    Private START_PATTERN_TIMESTAMP() As Byte = System.Text.Encoding.ASCII.GetBytes("$T,")
    Private END_PATTERN() As Byte = {&HD, &HA} ' = \r\n
    Private START_PATTERN_KLINE_OBD_MULTIFRAME() As Byte = {&H48, &H6B, &H10, &H49, &H2}

    ' Triggering
    Private TriggerEventCAN = New EventWaitHandle(False, EventResetMode.AutoReset)
    Private TriggerEventKLINE = New EventWaitHandle(False, EventResetMode.AutoReset)

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
    Private Shared CANMessageIDTriggerInterlock As New Object
    Private Shared CANMessageIDTriggerID As Integer
    Private Shared CANMessageIDTriggerCounter As Integer
    Private Shared CANMessageIDTriggerFlag As Boolean = False

    ' K-Line Message Handling
    Public ReadOnly MaxKLINEMessageBufferSize As Integer = 64
    Private KLINEMessagesIdx As Integer = 0
    Private Shared KLINEMessages(64) As KLINEMessage
    Private KLINEMessagesOverrun As Boolean = True
    Private Shared KLINEMessageTriggerFlag As Boolean = True

    ' $VER handling
    Private version_string As String = "unknown"

    ' $E handling
    Private error_code As Integer = 0

    ' $KSI handling
    Private kline_initalized As Integer = 0

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

    ' K-Line message structure
    Structure KLINEMessage
        Dim len As Integer
        Dim data() As Byte
        Dim crc As Byte
        Dim used As Boolean
        Dim status As Integer
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
    '* Connect to USB adapter
    '*****************************************************
    Public Function Connect(ByVal COMPortName As String, ByVal ResetArduino As Boolean) As Boolean
        If (COMPortName Is Nothing) Then
            Return (False)
        End If
        If (Me.is_open) Then
            Me.Disconnect()
        End If
        Try
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
            Me.is_open = True
            SendGetVersion()
        Catch ex As Exception
            Return (False)
        End Try
        Return (True)
    End Function

    '*****************************************************
    '* Connect to TCP adapter
    '*****************************************************
    Public Function Connect(ByVal IpAddress As String, ByVal Port As Integer) As Boolean
        If (Me.is_open) Then
            Me.Disconnect()
        End If
        Try
            'fs = New FileStream("C:\Users\Jacob\Source\Repos\T4e-ECU-Editor\bin\Debug\comlog.txt", FileMode.Create)
            'sw = New StreamWriter(fs)
            'sw.AutoFlush = True
            'Console.SetOut(sw)
            UsingSerial = False
            ShieldTimeout = 32767
            tcpClient = New AsyncSocket
            ' Next 2 lines for possible later implementation, if needed
            'tcpClient.ReceiveTimeout = ShieldTimeout
            'tcpClient.SendTimeout = ShieldTimeout
            tcpClient.Connect(IpAddress, Port)
            Me.is_open = True
            SendGetVersion()
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
    '* Reset KLINEMessages buffer handling 
    '*****************************************************
    Public Sub ResetKLINEMessages()
        SyncLock (KLINEMessages)
            For i As Integer = 0 To KLINEMessages.Length - 1
                KLINEMessages(i).used = False
            Next
        End SyncLock
        Interlocked.Exchange(KLINEMessagesIdx, 0)
        Interlocked.Exchange(KLINEMessagesOverrun, False)
    End Sub

    '*****************************************************
    '* Add one KLINEMessage to KLINEMessages array
    '*****************************************************
    Private Sub AddKLINEMessage(ByRef kmsg As KLINEMessage)
        'Dim oldsize As Integer = 0

        'Console.WriteLine("AddKLINEMessage()")
        SyncLock (KLINEMessages)
            KLINEMessages(Interlocked.Read(KLINEMessagesIdx)) = kmsg
            If (Interlocked.Read(KLINEMessagesIdx) < KLINEMessages.Length - 2) Then
                Interlocked.Increment(KLINEMessagesIdx)
            Else
                ' indicate overun of buffer                
                Interlocked.Exchange(KLINEMessagesOverrun, True)
            End If
        End SyncLock
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
    Private Sub AddCANMessage(ByVal cmsg As CANMessage)
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
        End SyncLock
    End Sub

    '*****************************************************
    '* Get copy of KLINEMessages array
    '*****************************************************
    Public Function GetKLINEMessagesBuffer() As KLINEMessage()
        Dim KLINEMessages_copy() As KLINEMessage = Nothing
        Dim idx = Interlocked.Read(KLINEMessagesIdx)

        Array.Resize(KLINEMessages_copy, idx)
        SyncLock (KLINEMessages)
            Array.Copy(KLINEMessages, KLINEMessages_copy, idx)
        End SyncLock
        Return (KLINEMessages_copy)
    End Function

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

    Public Function GetKLINEMessagesIdx() As Integer
        Dim r As Integer
        r = GetKLINEMessagesBuffersUsed()
        Return (r)
    End Function

    '*****************************************************
    '* return number of used buffers in KLINEMessages array
    '*****************************************************
    Private Function GetKLINEMessagesBuffersUsed() As Integer
        Dim count As Integer = 0
        count = Interlocked.Read(KLINEMessagesIdx)
        Return (count)
    End Function

    '*****************************************************
    '* return number of used buffers in CANMessages array
    '*****************************************************
    Private Function GetCANMessagesBuffersUsed() As Integer
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
    Public Function GetCANMessageIDTriggerCounter() As Integer
        Dim c As Integer
        c = getValue(CANMessageIDTriggerCounter)
        Return (c)
    End Function

    '*****************************************************
    '* reset the CAN Message ID Trigger flag
    '*****************************************************
    Private Sub ResetCANMessageIDTrigger()
        Interlocked.Exchange(CANMessageIDTriggerFlag, False)
        setValue(CANMessageIDTriggerCounter, 0)
    End Sub

    '*****************************************************
    '* reset the KLINE Message ID Trigger flag
    '*****************************************************
    Private Sub ResetKLINEMessageIDTrigger()
        Interlocked.Exchange(KLINEMessageTriggerFlag, False)
        'not used for now - setValue(KLINEMessageIDTriggerCounter, 0)
    End Sub

    '******************************************************************
    '* Wait for CAN Message ID Trigger ID being in the buffer
    '* returns true if triggered within timeout (ShieldTimeout msec)
    '* returns false if timeout happend while waiting for the right msg
    '******************************************************************
    Private Function WaitForCANMessageIDTrigger() As Boolean
        Dim sw As New Stopwatch

        sw.Reset()
        sw.Start()
        While (Interlocked.Read(CANMessageIDTriggerFlag) = False)
            TriggerEventCAN.WaitOne(New TimeSpan(0, 0, 1))
            If UsingSerial AndAlso (sw.ElapsedMilliseconds > ShieldTimeout) Then
#If DBG_ID_TRIGGER_TO Then
                Console.WriteLine("WaitForCANMessageIDTrigger(0x" & Hex(CANMessageIDTriggerID) & ") - timeout")
#End If
                Interlocked.Increment(StatRXWaitForIDTimeouts)
                Return (False)
            End If
        End While
        Return (True)
    End Function

    '******************************************************************
    '* Wait for KLINE Message ID Trigger ID being in the buffer
    '* returns true if triggered within timeout (ShieldTimeout msec)
    '* returns false if timeout happend while waiting for the right msg
    '******************************************************************
    Private Function WaitForKLINEMessageTrigger() As Boolean
        Dim sw As New Stopwatch

        sw.Reset()
        sw.Start()
        While (Interlocked.Read(KLINEMessageTriggerFlag) = False)
            TriggerEventKLINE.WaitOne(New TimeSpan(0, 0, 1))
            If (sw.ElapsedMilliseconds > ShieldTimeout) Then
                Interlocked.Increment(StatRXWaitForIDTimeouts)
                Interlocked.Exchange(KLINEMessageTriggerFlag, True)
                Return (False)
            End If
        End While
        Interlocked.Exchange(KLINEMessageTriggerFlag, True)
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
        Dim c As Integer = 10

        sw.Reset()
        sw.Start()
        While (getValue(CANMessageIDTriggerCounter) < num)
            If UsingSerial AndAlso (sw.ElapsedMilliseconds > ShieldTimeout) Then
#If DBG_ID_TRIGGER_TO Then
                Console.WriteLine("WaitForNumOfCANMessageIDTriggers(0x" & Hex(CANMessageIDTriggerID) & ") - TIMEOUT")
                Console.WriteLine(d & "/" & num & " in " & sw.ElapsedMilliseconds & " ms")
                'Console.WriteLine("TIMEOUT")
#End If
                Interlocked.Increment(StatRXWaitForIDTimeouts)
                Return (False)
                'With PiCan2, if car is not powered on, this will loop forever so if no messages received print a warning
            ElseIf Not UsingSerial AndAlso (getValue(CANMessageIDTriggerCounter) = 0) AndAlso (sw.ElapsedMilliseconds > (c * 1000)) Then
                Select Case MsgBox("Is car ignition on?", MsgBoxStyle.YesNo, "No Response")
                    Case MsgBoxResult.No
                        ' Close and Exit
                        Me.Disconnect()
                        Environment.Exit(0)
                    Case MsgBoxResult.Yes
                        ' Wait another minute
                        c += 60
                End Select
            End If
            TriggerEventCAN.WaitOne(New TimeSpan(0, 0, 0, 0, 1))
        End While
#If DBG_ID_TRIGGER_TO Then
        Console.WriteLine("WaitForNumOfCANMessageIDTriggers(0x" & Hex(CANMessageIDTriggerID) & ") - Success")
        Console.WriteLine(getValue(CANMessageIDTriggerCounter) & "/" & num & " in " & sw.ElapsedMilliseconds & " ms")
#End If
        Return (True)
    End Function


    Private Sub PrintBuf(buf() As Byte, ByVal dbg_name As String)
        If (buf.Length = 0) Then Return
        Dim buf_len As Integer = buf.Length
        Dim msg_str As String = ""
        If (buf_len > 0) Then
            For i = 0 To buf.Length - 1
                If (buf(i) < &H20) Or (buf(i) > &H7E) Then
                    msg_str &= " " & Hex(buf(i)) & "{" & i & "}"            ' non printable
                Else
                    msg_str &= Chr(buf(i)) ' & "{" & i & "}"            ' printable char
                End If

            Next
            Console.WriteLine(dbg_name & "(" & buf.Length & "): " & msg_str & "")
        Else
            Console.WriteLine(dbg_name & " buf() was empty")
        End If
    End Sub

    Private Function ExtractMessage(ByRef buf, ByRef str_start) As Byte()
        Dim str_end As Integer = 0
        Dim buf_copy() As Byte = {}
        Dim msg() As Byte = Nothing

        If (buf.length = 0) Then Return (New Byte() {})
        ' find end pattern \r\n
        str_end = SearchBytePattern(END_PATTERN, buf, str_start)
#If DBG_EXTRACT Then
        Console.WriteLine("")
        Console.WriteLine("=================================================================================")
        Console.WriteLine("ExtractMessage (" & str_start & "-" & str_end & ")")
#End If
        If (str_start < str_end) Then ' start and end found in buf, extract msg and cut out of buffer
            Dim buf_len = buf.length
            ' make a working copy of buf()
            Array.Resize(buf_copy, buf_len)
            Array.Copy(buf, buf_copy, buf_len)
            ' empty original buf()            
            buf = New Byte() {}
            Dim new_buf_size = buf_len - (str_end - str_start + END_PATTERN.Length) ' remainder buf len (after msg is extracted)
#If DBG_EXTRACT Then
            Console.WriteLine("Remainder Size: " & new_buf_size)
#End If
            If (new_buf_size > 0) Then
                Array.Resize(buf, new_buf_size)
                If (str_start > 0) Then                                               ' restore leading buf()
#If DBG_EXTRACT Then
                    Console.WriteLine("Leading (0-" & str_start & ")")
#End If
                    Array.Copy(buf_copy, buf, str_start)
                End If
                If (str_end < (buf_len - END_PATTERN.Length)) Then                   ' restore trailing buf()
#If DBG_EXTRACT Then
                    Console.WriteLine("Trailing (" & str_end + END_PATTERN.Length & "-" & str_end + END_PATTERN.Length + buf_copy.Length - (str_end + END_PATTERN.Length) & ")")
#End If
                    Array.Copy(buf_copy, str_end + END_PATTERN.Length, buf, str_start, buf_len - (str_end + END_PATTERN.Length)) ' add trailing part 
                End If
#If DBG_EXTRACT Then
                PrintBuf(buf, "ExtractMessage_REMAINDER")
#End If
            End If
            ' make some space for the extracted msg
            Array.Resize(msg, str_end - str_start)
            Array.Copy(buf_copy, str_start, msg, 0, str_end - str_start)        ' copy extracted message itself            
#If DBG_EXTRACT Then
            PrintBuf(msg, "ExtractMessage_MSG")
#End If
        End If
        If (str_end = -1) Then
#If DBG_EXTRACT Then
            Console.WriteLine("End not found !")
#End If
            str_start = -1
        End If
#If DBG_EXTRACT Then
        Console.WriteLine("=================================================================================")
        Console.WriteLine("")
#End If
        Return (msg)
    End Function

    Private Sub ExtractKnownMessages(ByRef buf)
        Dim s As Integer = 0
        Dim msg() As Byte = Nothing
        Dim crc As Integer = 0
        Dim stringSeparators() As String = {","}

#If DBG_EXTRACT Then
        PrintBuf(buf, "ExtractKnownMessages")
#End If
        '************************************************
        '* Extract $VER (Adapter Version Information)
        '************************************************
        s = SearchBytePattern(START_PATTERN_VER, buf, 0)
        While (s > -1)
            msg = ExtractMessage(buf, s)
            If (msg IsNot Nothing) Then
                Try
                    Dim ar() As String = System.Text.Encoding.ASCII.GetString(msg).Split(stringSeparators, StringSplitOptions.None)
                    version_string = ar(1)
                Catch ex As Exception
                    version_string = "error"
                End Try
            End If
            s = SearchBytePattern(START_PATTERN_VER, buf, s)
        End While

        '************************************************
        '* Extract $T (Timestamp) (Rasp)
        '************************************************
        s = SearchBytePattern(START_PATTERN_TIMESTAMP, buf, 0)
        While (s > -1)
            msg = ExtractMessage(buf, s)
            If (msg IsNot Nothing) Then
                Try
                    Dim ar() As String = System.Text.Encoding.ASCII.GetString(msg).Split(stringSeparators, StringSplitOptions.None)
                    ' timestamp = = ar(1)
                Catch ex As Exception
                    ' timestamp = error value ?
                End Try
            End If
            s = SearchBytePattern(START_PATTERN_TIMESTAMP, buf, s)
        End While

        '************************************************
        '* Extract $KSI (K-LINE Slow Init)
        '************************************************
        s = SearchBytePattern(START_PATTERN_KLINEINIT, buf, 0)
        While (s > -1)
            msg = ExtractMessage(buf, s)
            If (msg IsNot Nothing) Then
                Try
                    Dim ar() As String = System.Text.Encoding.ASCII.GetString(msg).Split(stringSeparators, StringSplitOptions.None)
                    kline_initalized = ar(1)
                Catch ex As Exception
                    kline_initalized = 0
                End Try
            End If
            s = SearchBytePattern(START_PATTERN_KLINEINIT, buf, s)
        End While

        '************************************************
        '* Extract $E (Error)
        '************************************************
        s = SearchBytePattern(START_PATTERN_ERROR, buf, 0)
        While (s > -1)
            msg = ExtractMessage(buf, s)
            If (msg IsNot Nothing) Then
                Try
                    Dim ar() As String = System.Text.Encoding.ASCII.GetString(msg).Split(stringSeparators, StringSplitOptions.None)
                    error_code = ar(1)
                Catch ex As Exception
                    error_code = 1
                End Try
                ErrorHandler(error_code)
            End If
            s = SearchBytePattern(START_PATTERN_ERROR, buf, s)
        End While

        '************************************************
        '* Extract $KO, (K-LINE OBD Message)
        '************************************************
        s = SearchBytePattern(START_PATTERN_KLINERX, buf, 0)
        While (s > -1)
            Dim kmsg As KLINEMessage
            kmsg = ExtractKLINEMessage(buf, s)
            If (kmsg.status >= 0) Then
                ' we need to check if its a single frame message or a multi frame message
                Dim mfs As Integer = SearchBytePattern(START_PATTERN_KLINE_OBD_MULTIFRAME, kmsg.data, 0)
                If (mfs = -1) Then
                    ''''''''''''''''''''''''''''''''''''
                    ' no match, must be single frame
                    ''''''''''''''''''''''''''''''''''''                    
                    For i As Integer = 0 To kmsg.len - 1
                        crc += kmsg.data(i) And &HFF
                    Next
                    'Console.WriteLine("CRC calced 0x" & Hex(crc And &HFF))
                    'Console.WriteLine("CRC msg 0x" & Hex(kmsg.crc))
                    If (kmsg.crc = (crc And &HFF)) Then
                        AddKLINEMessage(kmsg)
                    Else
                        Interlocked.Increment(StatRXCRCErrors)
                    End If

                Else
                    '''''''''''''''''''''''''''''''
                    ' match, must be multiframe
                    '''''''''''''''''''''''''''''''
                    Dim frame_count As Integer = 0
                    While (mfs > -1)
                        Dim kmsg_mf As New KLINEMessage ' single kmsg out of a multiframe msg
                        frame_count += 1
                        'Console.WriteLine("MULTIFRAME KLINE #" & frame_count & "/" & mfs & vbCrLf)
                        ' copy 10 databytes (single frame)
                        kmsg_mf.len = 10
                        Array.Resize(kmsg_mf.data, kmsg_mf.len)
                        Array.Copy(kmsg.data, mfs, kmsg_mf.data, 0, kmsg_mf.len)

                        ' For i As Integer = mfs To mfs + kmsg_mf.len - 1
                        '     Console.Write(" " & Hex(kmsg.data(i)))
                        ' Next
                        'Console.WriteLine("")
                        'Console.WriteLine("mfs " & mfs + kmsg_mf.len & " / " & kmsg.len)

                        If (mfs + kmsg_mf.len > kmsg.len - 1) Then       ' the last single frame's CRC is the one from kmsg.crc
                            kmsg_mf.crc = kmsg.crc
                        Else
                            kmsg_mf.crc = kmsg.data(mfs + 10)
                        End If
                        'Console.WriteLine(" CRC: 0x" & Hex(kmsg_mf.crc))

                        AddKLINEMessage(kmsg_mf)
                        mfs += START_PATTERN_KLINE_OBD_MULTIFRAME.Length
                        mfs = SearchBytePattern(START_PATTERN_KLINE_OBD_MULTIFRAME, kmsg.data, mfs)
                    End While
                End If
                ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
                ' set trigger - multiframe msgs only set trigger after all subframes are received
                ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
                Interlocked.Exchange(KLINEMessageTriggerFlag, True)
                TriggerEventKLINE.Set()
            Else ' there was an error extracting the KLINE Message
                Interlocked.Increment(StatRXShortMsgErrors)
                Exit While
            End If
            s = SearchBytePattern(START_PATTERN_KLINERX, buf, s)
        End While

        '************************************************
        '* Extract $F (CAN Message Frame)
        '************************************************
        s = SearchBytePattern(START_PATTERN_CANRX, buf, 0)
        While (s > -1)
            Dim cmsg As CANMessage
            cmsg = ExtractCANMessage(buf, s)
            If (cmsg.id >= 0) Then
                Interlocked.Increment(StatRXValidReceived)
                AddCANMessage(cmsg) ' message decoded fine, store for later use
                If (cmsg.id = Interlocked.Read(CANMessageIDTriggerID)) Then ' probe trigger id and set flag if matched 
                    Interlocked.Exchange(CANMessageIDTriggerFlag, True)
                    Interlocked.Increment(CANMessageIDTriggerCounter)
                    TriggerEventCAN.Set()
                End If
            Else ' there was an error extracting the CAN message
                If (cmsg.id < 0) Then
                    If (cmsg.id = -4) Then  ' CRC Error
                        Interlocked.Increment(StatRXCRCErrors)
                    End If
                    If (cmsg.id = -3) Then  ' Short message error
                        Interlocked.Increment(StatRXShortMsgErrors)
                    End If
                End If
            End If
            s = SearchBytePattern(START_PATTERN_CANRX, buf, s)
        End While
    End Sub

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
        Dim buf_len As Integer

        If ComPort.IsOpen Then
            Try
                buf_len = buf.Length
                Dim ser_len As Integer
                ser_len = ComPort.BytesToRead
                Array.Resize(buf, buf_len + ser_len)     ' make space for new data in buf()                
                ComPort.Read(buf, buf_len, ser_len)      ' append data from ComPort to buf()
                ExtractKnownMessages(buf)                ' extract (and remove) msgs from buf()             
                Console.WriteLine(buf.Length)
                Exit Sub
#If DBG_RX_IN Then
                msg_str = ""
                For i = 0 To buf.Length - 1
                    msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                Next
                Console.WriteLine("received_str(" & buf.Length & "): " & msg_str & "")
#End If
                '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
                ' try to extract CANMessages from buf
                ' find '$F' marker                
                ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
                ' Dim count As Integer = 0
                copy_start_idx = 0
                s = 0
                s = SearchBytePattern(START_PATTERN_CANRX, buf, s)
                While (s > -1)
                    cmsg = ExtractCANMessage(buf, s)
                    If (cmsg.id >= 0) Then
                        Interlocked.Increment(StatRXValidReceived)
#If DBG_RX_VALID Then
                        msg_str = ""
                        For i = s To s + cmsg.len + 6 ' buf.Length - 1
                            msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                        Next
                        Console.WriteLine("valid_msg(0x" & Hex(cmsg.id) & "/" & s & ") " & msg_str)
#End If
                        AddCANMessage(cmsg) ' message decoded fine, store for later use
                        If (cmsg.id = Interlocked.Read(CANMessageIDTriggerID)) Then ' probe trigger id and set flag if matched 
                            Interlocked.Exchange(CANMessageIDTriggerFlag, True)
                            Interlocked.Increment(CANMessageIDTriggerCounter)
                            TriggerEventCAN.Set()
#If DBG_ID_TRIGGER Then
                            Console.WriteLine("CANMessageIDTriggerID() Matched")
#End If
                        End If
                        'count = count + 1
                        copy_start_idx = s + cmsg.len + 7
                    Else ' there was an error extracting the CAN message
                        If (cmsg.id < 0) Then
                            If (cmsg.id = -4) Then  ' CRC Error
                                Interlocked.Increment(StatRXCRCErrors)
                            End If
                            If (cmsg.id = -3) Then  ' Short message error
                                Interlocked.Increment(StatRXShortMsgErrors)
                            End If
#If DBG_RX_ERR4 Then
                            msg_str = ""
                            For i = s To buf.Length - 1 ' s + 14
                                msg_str &= " " & Hex(buf(i)) & "{" & i & "}"
                            Next
                            Console.WriteLine("invalid_msg(" & cmsg.id & "/" & s & "/" & buf.Length & ") " & msg_str)
#End If
                        End If
                        copy_start_idx = s
                    End If
                    s = SearchBytePattern(START_PATTERN_CANRX, buf, s + START_PATTERN_CANRX.Length) ' find the next pattern
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

    Private Function getValue(ByRef o As Integer)
        SyncLock (CANMessageIDTriggerInterlock)
            Return Interlocked.Read(o)
        End SyncLock
    End Function
    Private Sub incValue(ByRef o As Integer)
        SyncLock (CANMessageIDTriggerInterlock)
            Interlocked.Increment(o)
        End SyncLock
    End Sub
    Private Sub setValue(ByRef o As Integer, ByVal v As Integer)
        SyncLock (CANMessageIDTriggerInterlock)
            Interlocked.Exchange(o, v)
        End SyncLock
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
                s = SearchBytePattern(START_PATTERN_CANRX, buf, s)
                While (s > -1)
                    cmsg = ExtractCANMessage(buf, s)
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
                            incValue(CANMessageIDTriggerCounter)
                            TriggerEventCAN.Set()
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
                    s = SearchBytePattern(START_PATTERN_CANRX, buf, s + START_PATTERN_CANRX.Length)
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
                MsgBox(ex.Message & vbCrLf & ex.StackTrace)
                Exit Sub
            End Try
        End If
    End Sub

    '*****************************************************
    '* Close TCP connection
    '*****************************************************
    Private Sub tcpClient_socketDisconnected(ByVal SocketID As String) Handles tcpClient.socketDisconnected
        tcpClient.IsOpen = False
        Me.Disconnect()
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
    Private Function ExtractCANMessage(ByRef buf() As Byte, ByRef start As Integer) As CANMessage
        Dim cmsg As New CANMessage
        Dim i As Short
        Dim crc As Integer = 0

#If DBG_EXTRACT_CAN Then
        Console.WriteLine("ExtractCANMessage(" & start & "-?)")
#End If
        cmsg.used = False
        cmsg.id = -1
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' CAN frame with no payload is 8 bytes, so this is the minimum length
        ' of Bytes() otherwise we are fooked
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        If (buf.Length < (start + 7)) Then
            start = -1
#If DBG_EXTRACT_CAN Then
            Console.WriteLine("ExtractCANMessage - Minimum length not reached")
#End If
            Return (cmsg)
        End If

        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract .len and prepare .data
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        cmsg.len = buf(start + START_PATTERN_CANRX.Length) / 16
        ' make sure overall message size is long enough now that we know how long the .data is
        If (buf.Length < (start + cmsg.len + 7)) Then
            cmsg.id = -2
            start = -1
#If DBG_EXTRACT_CAN Then
            Console.WriteLine("ExtractCANMessage - Minimum length not reached after datalen is known")
#End If
            Return (cmsg)
        End If
        Array.Resize(cmsg.data, cmsg.len)

        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract .id
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        cmsg.id = (buf(start + START_PATTERN_CANRX.Length) And &HF) * 256 Or buf(start + START_PATTERN_CANRX.Length + 1)
        crc += buf(start + START_PATTERN_CANRX.Length) And &HFF
        crc += cmsg.id And &HFF

        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract .data
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        For i = 0 To cmsg.len - 1
            cmsg.data(i) = buf(start + START_PATTERN_CANRX.Length + 2 + i)
            crc += cmsg.data(i)
        Next

        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Check location of CRC and its value
        '''''''''''''''''''''''''''''''''''''''''''''''''''        
        If (buf.Length < (start + START_PATTERN_CANRX.Length + 2 + cmsg.len)) Then   ' check if the message is long enough to have a CRC byte
            cmsg.id = -3
            start = -1
#If DBG_EXTRACT_CAN Then
            Console.WriteLine("ExtractCANMessage - Not long enough for CRC")
#End If
            Return (cmsg)
        Else
            ' check CRC
            If (buf(start + START_PATTERN_CANRX.Length + 2 + cmsg.len) <> (crc And &HFF)) Then
                Console.WriteLine("ExtractCANMessage not matching expected calced=0x" & Hex(crc And &HFF) & " expected=0x" & Hex(buf(start + START_PATTERN_CANRX.Length + 2 + cmsg.len)))
                cmsg.id = -4
                start = -1
#If DBG_EXTRACT_CAN Then
                Console.WriteLine("ExtractCANMessage - Invalid CRC")
#End If
                Return (cmsg)
            End If
        End If

        ''''''''''''''''''''''''''''''''''''''''''''''
        ' indicate successful extraction
        ''''''''''''''''''''''''''''''''''''''''''''''
        cmsg.used = True
        '''''''''''''''''''''''''''''''''''''''''''
        ' truncate buf()
        '''''''''''''''''''''''''''''''''''''''''''
        Dim str_end, str_start
        Dim buf_copy() As Byte = {}
        str_start = start
        str_end = start + 4 + cmsg.len + 1      ' $,F,len,id,crc + data
        Dim buf_len = buf.Length
        ' make a working copy of buf()
        Array.Resize(buf_copy, buf_len)
        Array.Copy(buf, buf_copy, buf_len)
        ' empty original buf()            
        buf = New Byte() {}
        Dim new_buf_size = buf_len - (str_end - str_start + END_PATTERN.Length)     ' remainder buf len (after msg is extracted)
#If DBG_EXTRACT_CAN Then
        Console.WriteLine("Remainder Size: " & new_buf_size)
#End If
        If (new_buf_size > 0) Then
            Array.Resize(buf, new_buf_size)
            If (str_start > 0) Then                                               ' restore leading buf()
#If DBG_EXTRACT_CAN Then
                Console.WriteLine("Leading (0-" & str_start & ")")
#End If
                Array.Copy(buf_copy, buf, str_start)
            End If
            If (str_end < (buf_len - END_PATTERN.Length)) Then                   ' restore trailing buf()
#If DBG_EXTRACT_CAN Then
                Console.WriteLine("Trailing (" & str_end + END_PATTERN.Length & "-" & str_end + END_PATTERN.Length + buf_copy.Length - (str_end + END_PATTERN.Length) & ")")
#End If
                Array.Copy(buf_copy, str_end + END_PATTERN.Length, buf, str_start, buf_len - (str_end + END_PATTERN.Length)) ' add trailing part 
            End If
#If DBG_EXTRACT_CAN Then
            PrintBuf(buf, "ExtractCANMessage_REMAINDER")
#End If
        End If
        Return (cmsg)
    End Function

    '*****************************************************
    '* Extract received $K KLINE message and store into
    '* KLINEMessage structure element
    '*****************************************************
    Private Function ExtractKLINEMessage(ByRef buf() As Byte, ByRef start As Integer) As KLINEMessage
        Dim kmsg As New KLINEMessage
        Dim i As Short
        Dim crc As Integer = 0
        Dim str_end As Integer

#If DBG_EXTRACT_KLINE Then
        Console.WriteLine("ExtractKLINEMessage(" & start & ")")
        PrintBuf(buf, "ExtractKLINEMessage")
#End If
        kmsg.used = False
        kmsg.status = -1
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract len byte from message and make sure we have the full frame        
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        If (buf.Length < (start + START_PATTERN_KLINERX.Length + 1)) Then
            start = start + 1
#If DBG_EXTRACT_KLINE Then
            Console.WriteLine("ExtractKLINEMessage - Frame length not available")
#End If
            Return (kmsg)
        End If

        Dim len_raw As Integer = buf(start + START_PATTERN_KLINERX.Length)      ' data length (incl. CRC/last byte)

        If (buf.Length < (start + START_PATTERN_KLINERX.Length + 1 + len_raw + END_PATTERN.Length)) Then
            start = start + 1
#If DBG_EXTRACT_KLINE Then
            Console.WriteLine("ExtractKLINEMessage - Complete frame length not available")
#End If
            Return (kmsg)
        End If

        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract .len and prepare .data            
        '''''''''''''''''''''''''''''''''''''''''''''''''''            
        str_end = SearchBytePattern(END_PATTERN, buf, start)
        If (str_end = -1) Then ' no END_PATTERN found
            start = start + 1
#If DBG_EXTRACT_KLINE Then
            Console.WriteLine("ExtractKLINEMessage - No END pattern found")
#End If
            Return (kmsg)
        End If

        kmsg.len = len_raw - 1
        Array.Resize(kmsg.data, kmsg.len)
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Extract .data and .crc (CRC is not checked here)
        '''''''''''''''''''''''''''''''''''''''''''''''''''
        For i = 0 To kmsg.len - 1
            kmsg.data(i) = buf(start + START_PATTERN_KLINERX.Length + 1 + i)
        Next
        kmsg.crc = buf(start + START_PATTERN_KLINERX.Length + 1 + kmsg.len)

        ''''''''''''''''''''''''''''''''''
        ' indicate successful extraction
        ''''''''''''''''''''''''''''''''''
        kmsg.used = True
        kmsg.status = 0

        '''''''''''''''''''''''''''''''''''''''''''
        ' truncate buf()
        '''''''''''''''''''''''''''''''''''''''''''
        Dim str_start
        Dim buf_copy() As Byte = {}
        str_start = start
        Dim buf_len = buf.Length
        ' make a working copy of buf()
        Array.Resize(buf_copy, buf_len)
        Array.Copy(buf, buf_copy, buf_len)
        ' empty original buf()            
        buf = New Byte() {}
        Dim new_buf_size = buf_len - (str_end - str_start + END_PATTERN.Length)     ' remainder buf len (after msg is extracted)
#If DBG_EXTRACT_KLINE Then
        Console.WriteLine("Remainder Size: " & new_buf_size)
#End If
        If (new_buf_size > 0) Then
            Array.Resize(buf, new_buf_size)
            If (str_start > 0) Then                                               ' restore leading buf()
#If DBG_EXTRACT_KLINE Then
                Console.WriteLine("Leading (0-" & str_start & ")")
#End If
                Array.Copy(buf_copy, buf, str_start)
            End If
            If (str_end < (buf_len - END_PATTERN.Length)) Then                   ' restore trailing buf()
#If DBG_EXTRACT_KLINE Then
                Console.WriteLine("Trailing (" & str_end + END_PATTERN.Length & "-" & str_end + END_PATTERN.Length + buf_copy.Length - (str_end + END_PATTERN.Length) & ")")
#End If
                Array.Copy(buf_copy, str_end + END_PATTERN.Length, buf, str_start, buf_len - (str_end + END_PATTERN.Length)) ' add trailing part 
            End If
#If DBG_EXTRACT_KLINE Then
            PrintBuf(buf, "ExtractCANMessage_REMAINDER")
#End If
        End If
        Return (kmsg)
    End Function

    '*****************************************************
    '* search for pattern in Bytes() starting from start 
    '* offset - used to identfy data blocks in $F messages   
    '*****************************************************
    Public Function SearchBytePattern(ByRef pattern() As Byte, ByRef Bytes() As Byte, ByVal start As Integer) As Integer

        If (Bytes Is Nothing) Then Return -1
        If (start = -1) Then Return -1

        Dim matches As Integer = -1
        Dim i, j As Integer
        Dim ismatch As Boolean
        Dim maxloop As Integer = Bytes.Length - pattern.Length + 1     ' precomputing this shaves some seconds from the loop execution

#If DBG_EXTRACT_SEARCH Then
        Console.Write("SearchBytePattern(" & start & "-" & maxloop & "): ")
        For i = 0 To pattern.Length - 1
            Console.Write(" 0x" & Hex(pattern(i)))
        Next i
        Console.WriteLine("")
#End If
        For i = start To maxloop - 1
            If (pattern(0) = Bytes(i)) Then         ' first byte in pattern matches in Bytes
#If DBG_EXTRACT_SEARCH Then
                Console.WriteLine(" * SearchBytePattern 0x" & Hex(pattern(0)) & " matched at " & i)
#End If
                ismatch = True
                For j = 1 To pattern.Length - 1
                    If (Bytes(i + j) <> pattern(j)) Then
                        ismatch = False
                        Exit For
#If DBG_EXTRACT_SEARCH Then
                    Else
                        Console.WriteLine(" * SearchBytePattern 0x" & Hex(pattern(j)) & " matched at " & i + j)
#End If
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
        Return (version_string)
    End Function

    '*****************************************************
    '* Trigger $VER query to populate "version_string"
    '*****************************************************
    Public Sub SendGetVersion()
        Dim query As String = "$VER" & vbCrLf
        If (UsingSerial AndAlso ComPort.IsOpen) Then
            ComPort.Write(query)
        End If
        If (Not UsingSerial AndAlso tcpClient.IsOpen) Then
            tcpClient.Send(query)
        End If
    End Sub

    '*****************************************************
    '* Trigger K-LINE Slow Init ($KSI)
    '*****************************************************
    Public Sub SendKLINEInit()
        Dim query As String = "$KSI" & vbCrLf
        If (UsingSerial AndAlso ComPort.IsOpen) Then
            ComPort.Write(query)
        End If
    End Sub

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


    '*****************************************************
    '* Trigger K-Line Stage2 Bootloader catch routine
    '*****************************************************
    Public Function KLineCatchStage2() As Boolean
        If (UsingSerial AndAlso ComPort.IsOpen) Then
            Dim query As String = ""
            query = "$B2" & vbCrLf
            ComPort.Write(query)
            Return (True)
        End If
        Return (False)
    End Function

    '*****************************************************
    '* Send KLINE OBD request (and wait for reply)
    '*****************************************************
    Public Function SendAndWaitForKLINEMessage(ByVal Request As KLINEMessage) As Boolean
        Dim r As Boolean = False
        Dim crc As Integer = 0

        ' check if kline is currently initalized
        If (kline_initalized <> 1) Then
            Return (False)
        End If
        ' check if the previous msg has been received
        If (Interlocked.Read(KLINEMessageTriggerFlag) = False) Then
            Return (False)
        End If

        If (is_open) Then
            If (UsingSerial AndAlso ComPort.IsOpen) Then
                Dim query As String = "$KO," & Hex(Request.len + 1)
                For i As Integer = 0 To Request.len - 1
                    query &= "," & Hex(Request.data(i))
                    crc += Request.data(i)
                Next
                Request.crc = crc And &HFF
                query &= "," & Hex(Request.crc)
                query &= vbCrLf
                ResetKLINEMessages()
                ResetKLINEMessageIDTrigger()
                ComPort.Write(query)
                r = WaitForKLINEMessageTrigger()
            End If
        End If
        Return (r)
    End Function


    '*****************************************************
    '* Process any $E messages if there is something to do 
    '*****************************************************
    '* Codes
    '*
    '* 0XXX (Adapter)
    '* 1XXX (CAN)
    '* 2XXX (KLINE)
    '*
    '* 2001 - $K    2s timeout while waiting for KLINE OBD response
    '* 2002 - $K    no reply received for KLINE OBD response
    '* 2003 - $K    number of , is invalid (too short, too long)
    '* 2004 - $K    data len is less than 1 or more than 7
    '* 2011 - $KSI - did not receive ECU's 0x55 reply within 2s of slow init sequence
    '* 2012 - $KSI - did not receive 0xCC acknowledge to inverted KW2
    Private Sub ErrorHandler(ByVal error_code As Integer)
#If DBG_ERROR_HANDLER Then
        Console.writeline("Error: " & error_code)
#End If
        Select Case error_code
            Case 2001 To 2002               ' we lost K-Line sync ?
                kline_initalized = 0
            Case Else
                ' nothing
        End Select
    End Sub
End Class


