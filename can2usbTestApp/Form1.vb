'*******************************************************************************
'* testbed for can2usb 
'*
'*
'*******************************************************************************
Imports System.Windows.Forms.DataVisualization.Charting

Public Class Form1
    'Dim adapter As New can2usbDLL.can2usb
    Dim burstcounter As Integer = 0
    Dim memoryreadcounter As Integer = 0
    ' hooks for ECU connection, and registry
    Public Shared T4eReg As New T4eRegistry
    Public Shared ECU As New ECU

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        ECUConnect.Show()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        ECU.Adapter.Disconnect()
    End Sub

    ' clear textbox
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        TextBox1.Text = ""
    End Sub

    Private Sub BBurst_Click(sender As Object, e As EventArgs) Handles BBurst.Click
        SimulateCAN80Burst()
    End Sub

    ' update statistics groupbox
    Private Sub TimerStats_Tick(sender As Object, e As EventArgs) Handles TimerStats.Tick
        ' update stat counters
        If (ECU.Adapter.isConnected) Then
            TBRXValidReceived.Text = ECU.Adapter.GetRXValidReceived()
            TBRXCRCErrors.Text = ECU.Adapter.GetRXCRCErrors()
            TBMessagesSent.Text = ECU.Adapter.GetMessagesSent()
            TBRXShortMsgErrors.Text = ECU.Adapter.GetRXShortMsgErrors()
            TBRXWaitForIDTimeouts.Text = ECU.Adapter.GetRXWaitForIDTimeouts()
            TBTriggerHit.Text = ECU.Adapter.GetCANMessageIDTrigger
            TBCANMessagesIdx.Text = ECU.Adapter.GetCANMessagesIdx()
            TBBurstCount.Text = burstcounter
            TBMemoryReadCounter.Text = memoryreadcounter
        Else
            TBRXValidReceived.Text = "not connected"
            TBRXCRCErrors.Text = "not connected"
            TBMessagesSent.Text = "not connected"
            TBRXShortMsgErrors.Text = "not connected"
            TBRXWaitForIDTimeouts.Text = "not connected"
            TBTriggerHit.Text = "not connected"
            TBCANMessagesIdx.Text = "not connected"
            TBBurstCount.Text = "not connected"
            TBMemoryReadCounter.Text = "not connected"
        End If
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If (T4eReg.GetECUAutoConnect) Then
            If (Not ECU.Adapter.isConnected) Then
                ECUConnect.ConnectToECU(T4eReg.GetECUComPort, T4eReg.GetECUCANSpeed, T4eReg.GetECUCANShieldType)
            End If
        End If
        LPollInterval.Text = SBPollInterval.Value & " ms"
    End Sub

    Private Sub BMemoryRead_Click(sender As Object, e As EventArgs) Handles BMemoryRead.Click
        SimulateMemoryRead()
    End Sub


    '*****************************************************
    '* Simulate CAN 0x80 message burst
    '*****************************************************
    Private Sub SimulateCAN80Burst()
        Dim cmsg As New can2usbDLL.can2usb.CANMessage
        cmsg.id = &H80
        cmsg.len = 3
        Array.Resize(cmsg.data, cmsg.len)
        cmsg.data(0) = 8
        cmsg.data(1) = 1
        ECU.Adapter.SendAndWaitForCANMessageID(cmsg, &H2B4)
        AddToTBByID(1, &H2B4)       ' &H2B4 is added to textbox if we catch it
        burstcounter += 1
    End Sub


    '*****************************************************
    '* Simulate CAN 0x50 memory reading
    '*****************************************************
    Private Sub SimulateMemoryRead()
        Dim addr As Integer = &H10000
        Dim len As Integer = 40
        'Dim stepsize As Integer = 248
        Dim stepsize As Integer = 40
        Dim b() As Byte
        Dim bytes_idx As Integer = 0
        Dim retrycounter As Integer = 0

        For i As Integer = addr To addr + len - stepsize Step stepsize
            'Console.WriteLine("SimulateMemoryRead() addr =  " & Hex(i))
            b = ECU.ECUReadMemory(i, stepsize)
            memoryreadcounter += 1
            If (b IsNot Nothing) Then
                bytes_idx += b.Length
                TextBox1.Text &= System.Text.Encoding.ASCII.GetString(b)
                TextBox1.Text &= vbCrLf
            End If
        Next
        'Console.WriteLine("SimulateMemoryRead() read " & bytes_idx)
    End Sub

    '*****************************************************
    '* Simulate OBD Mode 0x22 call to fetch ECU type
    '*****************************************************
    Private Sub SimulateOBDRead()
        Dim b() As Byte

        b = ECU.ECUQueryOBD(&H22, &H211)
        If (b IsNot Nothing) Then
            TextBox1.Text &= System.Text.Encoding.ASCII.GetString(b) & vbCrLf
        Else
            TextBox1.Text &= "no reply to OBD query" & vbCrLf
        End If
    End Sub

    Private Sub AddToTBByID(ByVal addr, ByVal id)
        Dim cb() As can2usbDLL.can2usb.CANMessage
        cb = ECU.Adapter.GetCANMessagesBuffer()
        For i = 0 To cb.Length - 1
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
                SimulateOBDRead()
            End If
        End If
    End Sub

    Private Sub SBPollInterval_Scroll(sender As Object, e As ScrollEventArgs) Handles SBPollInterval.Scroll
        TimerQuery.Interval = SBPollInterval.Value
        LPollInterval.Text = SBPollInterval.Value & " ms"
    End Sub


    Private Sub BOBDRead_Click(sender As Object, e As EventArgs) Handles BOBDRead.Click
        SimulateOBDRead()
    End Sub
End Class
