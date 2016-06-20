'*******************************************************************************
'* testbed for can2usb 
'*
'*
'*******************************************************************************
Imports System.Windows.Forms.DataVisualization.Charting

Public Class Form1
    Dim adapter As New can2usbDLL.can2usb
    Dim burstcounter As Integer = 0
    Dim memoryreadcounter As Integer = 0



    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        adapter.Connect("COM3", True)
        adapter.Init(500, can2usbDLL.can2usb.ShieldType.SparkFun)
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        adapter.Disconnect()
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        TextBox1.Text = ""
    End Sub

    Private Sub BBurst_Click(sender As Object, e As EventArgs) Handles BBurst.Click
        'SimulateCAN80Burst()
        '        PollTypeOBD(&H22, &H211)
        ECUReadMemory(&H10000, 248)
    End Sub

    ' update statistics groupbox
    Private Sub TimerStats_Tick(sender As Object, e As EventArgs) Handles TimerStats.Tick
        ' update stat counters
        TBRXValidReceived.Text = adapter.GetRXValidReceived()
        TBRXCRCErrors.Text = adapter.GetRXCRCErrors()
        TBMessagesSent.Text = adapter.GetMessagesSent()
        TBRXShortMsgErrors.Text = adapter.GetRXShortMsgErrors()
        TBRXWaitForIDTimeouts.Text = adapter.GetRXWaitForIDTimeouts()
        TBTriggerHit.Text = adapter.GetCANMessageIDTrigger
        TBCANMessagesIdx.Text = adapter.GetCANMessagesIdx()
        TBBurstCount.Text = burstcounter
        TBMemoryReadCounter.Text = memoryreadcounter
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        adapter.Connect("COM3", True)
        adapter.Init(500, can2usbDLL.can2usb.ShieldType.SparkFun)
        LPollInterval.Text = SBPollInterval.Value
    End Sub

    Private Sub BMemoryRead_Click(sender As Object, e As EventArgs) Handles BMemoryRead.Click
        SimulateMemoryRead()
    End Sub


    '*****************************************************
    '* CAN 0x80 message burst
    '*****************************************************
    Private Sub SimulateCAN80Burst()
        TextBox1.Text = ""
        Dim cmsg As New can2usbDLL.can2usb.CANMessage
        cmsg.id = &H80
        cmsg.len = 3
        Array.Resize(cmsg.data, cmsg.len)
        cmsg.data(0) = 8
        cmsg.data(1) = 1
        adapter.SendAndWaitForCANMessageID(cmsg, &H2B4)
        AddToTBByID(1, &H2B4)
        burstcounter += 1
    End Sub


    '*****************************************************
    '* Simulate CAN 0x50 memory reading
    '*****************************************************
    Private Sub SimulateMemoryRead()
        Dim addr As Integer = &H10000
        Dim len As Integer = 24
        'Dim stepsize As Integer = 248
        Dim stepsize As Integer = 24
        Dim b() As Byte
        Dim bytes_idx As Integer = 0
        Dim retrycounter As Integer = 0

        For i As Integer = addr To addr + len - stepsize Step stepsize
            Console.WriteLine("Downloader()  " & Hex(i))
            b = ECUReadMemory(i, stepsize)
            memoryreadcounter += 1
            If (b IsNot Nothing) Then
                bytes_idx += b.Length
            End If
        Next
        Console.WriteLine("Downloader() read " & bytes_idx)
    End Sub

    Private Sub AddToTBByID(ByVal addr, ByVal id)
        Dim cb() As can2usbDLL.can2usb.CANMessage

        cb = adapter.GetCANMessagesBuffer()
        For i = 0 To cb.Length - 2
            If (cb(i).id = id) Then
                TextBox1.Text &= addr & " = " & Hex(cb(i).id) & vbCrLf
            End If
        Next
    End Sub



    '*****************************************************
    '* Timer Event, based on selected tests do some action
    '*****************************************************
    Private Sub TimerQuery_Tick(sender As Object, e As EventArgs) Handles TimerQuery.Tick
        If (CBTimerQuery.Checked) Then
            If (CBCAN50.Checked) Then
                SimulateMemoryRead()
            End If
            If (CBCAN80.Checked) Then
                SimulateCAN80Burst()
            End If
            If (CBCANOBD.Checked) Then
                PollTypeOBD(&H22, &H211)
            End If
        End If
    End Sub

    Private Sub SBPollInterval_Scroll(sender As Object, e As ScrollEventArgs) Handles SBPollInterval.Scroll
        TimerQuery.Interval = SBPollInterval.Value
        LPollInterval.Text = SBPollInterval.Value
    End Sub



    '*****************************************************
    '* Read OBD Mode and Pid
    '*****************************************************
    Public Function PollTypeOBD(ByVal Mode As Integer, ByVal Pid As Integer) As Byte()
        Dim retval() As Byte = Nothing
        Dim can_len As Integer = 0
        Dim obd_tx_len As Integer = 0
        Dim canline As String = ""

        Dim obd_cmd As String


        If (Not adapter.isConnected) Then
            Return (retval)
        End If
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' calculate size of PID to see if we need 1 or 2 bytes
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''
        If (Pid > &HFF) Then
            obd_tx_len = 3
            obd_cmd = "$S," & 1 + obd_tx_len & ",7DF," & Hex(obd_tx_len) & "," & Hex(Mode) & "," & Hex(Pid >> 8) & "," & Hex(Pid And &HFF)
        Else
            obd_tx_len = 2
            obd_cmd = "$S," & 1 + obd_tx_len & ",7DF," & Hex(obd_tx_len) & "," & Hex(Mode) & "," & Hex(Pid)
        End If

        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' create CANMessage struct and send
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        Dim cmsg As New can2usbDLL.can2usb.CANMessage
        cmsg.id = &H7DF
        cmsg.len = obd_tx_len + 1
        Array.Resize(cmsg.data, cmsg.len)
        cmsg.data(0) = obd_tx_len
        cmsg.data(1) = Mode
        If (Pid > &HFF) Then
            cmsg.data(2) = Pid >> 8
            cmsg.data(3) = Pid And &HFF
        Else
            cmsg.data(2) = Pid
        End If
        Console.WriteLine("len: " & cmsg.len)
        For i As Integer = 0 To cmsg.len - 1
            Console.WriteLine(Hex(cmsg.data(i)))
        Next
        Console.WriteLine(obd_cmd)
        adapter.SendAndWaitForCANMessageID(cmsg, &H7E8)
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' process reply
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        Dim cb() As can2usbDLL.can2usb.CANMessage
        cb = adapter.GetCANMessagesBuffer
        For i As Integer = 0 To cb.Length - 1
            Console.WriteLine("OBD CAN buffer: " & Hex(cb(i).id))
        Next

        cmsg = adapter.GetFirstCANMessageBufferByID(&H7E8)
        If (cmsg.used = 0) Then
            Return (Nothing)
        End If

        Dim obd_rx_mode As Integer = cmsg.data(0)
        Dim obd_rx_pid As Integer = 0
        Dim obd_rx_len As Integer = cmsg.len
        Dim obd_rx_datalen As Integer = obd_rx_len - obd_tx_len
        If (obd_tx_len = 2) Then
            obd_rx_pid = cmsg.data(1)
        ElseIf (obd_tx_len = 3) Then
            obd_rx_pid = cmsg.data(1) << 8 Or cmsg.data(2)
        End If
        Console.WriteLine("PollTypeOBD() Reply Mode: 0x" & Hex(obd_rx_mode))
        Console.WriteLine("PollTypeOBD() Reply PID: 0x" & Hex(obd_rx_pid))
        Console.WriteLine("PollTypeOBD() Reply Data Size: 0x" & Hex(obd_rx_datalen))
        If (obd_rx_pid = Pid) And (obd_rx_mode - &H40 = Mode) Then
            ' successful OBD reply                    
            Array.Resize(retval, obd_rx_len - obd_tx_len)
            For i As Integer = 0 To obd_rx_datalen - 1
                retval(i) = cmsg.data(obd_tx_len + i)
            Next
            Return (retval)
        End If
        Return (retval)
    End Function


    '*****************************************************
    '* Read ECU Memory of size (0xFF max) from addr
    '*****************************************************
    Public Function ECUReadMemory(ByVal addr As Integer, ByVal size As Byte) As Byte()
        Dim retval() As Byte = Nothing
        Dim len As Integer = 0
        Dim status As Boolean
        Dim a() As Byte = BitConverter.GetBytes(addr)

        If (Not adapter.isConnected) Then
            Return (retval)
        End If
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' create CANMessage struct and send
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        Dim cmsg As New can2usbDLL.can2usb.CANMessage
        cmsg.id = &H53
        cmsg.len = 5
        Array.Resize(cmsg.data, cmsg.len)
        cmsg.data(0) = a(3)
        cmsg.data(1) = a(2)
        cmsg.data(2) = a(1)
        cmsg.data(3) = a(0)
        cmsg.data(4) = size And &HFF
        status = adapter.SendAndWaitForNumOfCANMessageIDs(cmsg, &H7A0, size / 8)
        'If (status) Then
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' process reply
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 
        ' allocate memory needed for fully reply
        Array.Resize(retval, size)
        Dim retval_idx As Integer = 0

        Dim cb() As can2usbDLL.can2usb.CANMessage
            cb = adapter.GetCANMessagesBuffer
            For i As Integer = 0 To cb.Length - 1
            If (cb(i).id = &H7A0) Then
                If (retval_idx < size - cb(i).len) Then
                    Array.Copy(cb(i).data, 0, retval, retval_idx, cb(i).len)
                    retval_idx += cb(i).len
                End If
                Console.Write(i & "*** 0x" & Hex(cb(i).id))
                For j As Integer = 0 To cb(i).len - 1
                    Console.Write(" " & Hex(cb(i).data(j)))
                Next
                Console.WriteLine("")
            Else
                Console.Write(i & "    0x" & Hex(cb(i).id))
                For j As Integer = 0 To cb(i).len - 1
                    Console.Write(" " & Hex(cb(i).data(j)))
                Next
                Console.WriteLine("")
            End If

        Next
        'End If
        Console.WriteLine("--------------------------------------------------------")
        Return (retval)
    End Function


End Class
