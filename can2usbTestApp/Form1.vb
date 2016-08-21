'*******************************************************************************
'* testbed for can2usb 
'* (c) Georg Swoboda 2016 <cn@warp.at>
'*******************************************************************************
'Imports System.Windows.Forms.DataVisualization.Charting
Imports System.IO
Imports System.Security
Imports System.Security.Cryptography

Public Class Form1
    'Dim adapter As New can2usbDLL.can2usb
    Dim burstcounter As Integer = 0
    Dim memoryreadcounter As Integer = 0
    Dim binpath As String = Application.StartupPath
    ' hooks for ECU connection, and registry
    Public Shared T4eReg As New T4eRegistry
    Public Shared ECU As New ECU

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        ECUConnect.Show()
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        ECU.Adapter.Disconnect()
        UpdateControls()
    End Sub

    ' clear textbox
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        TextBox1.Text = ""
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

        ' clear out textbox1 if it gets too large (so we can run it continously without crashing)
        If (TextBox1.Text.Split(vbCrLf).Length > 250) Then
            TextBox1.Text = ""  ' clear if we have more than 1000 lines in it
        End If
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LblCopyright.Text = "V " & Application.ProductVersion & " (c) cn@warp.at, 2016"
        If (T4eReg.GetECUAutoConnect) Then
            If (Not ECU.Adapter.isConnected) Then
                If (T4eReg.GetECUCANShieldType = can2usbDLL.can2usb.ShieldType.PiCAN2) Then
                    ECUConnect.ConnectToECU(T4eReg.GetECUIPAddress, T4eReg.GetECUTCPPort, T4eReg.GetECUCANSpeed, T4eReg.GetECUCANShieldType)
                Else
                    ECUConnect.ConnectToECU(T4eReg.GetECUComPort, T4eReg.GetECUCANSpeed, T4eReg.GetECUCANShieldType)
                End If
                UpdateControls()
            End If
        End If
        LPollInterval.Text = SBPollInterval.Value & " ms"
    End Sub


    '*****************************************************
    '* Update available controls based on ECU capabilities
    '*****************************************************
    Public Sub UpdateControls()
        If (ECU.Adapter.isConnected) Then
            If (ECU.AccessLevel = ECU.ECUAccessLevel.KLINE_CAN) Then ' KLINE/CAN ECU (no OBD possible without KLINE adapter)
                CBCAN50.Enabled = True
                BDownloadECU.Enabled = True
                CBCAN80.Enabled = True
                CBCANOBD.Enabled = False
                CBTimerQuery.Enabled = True
            ElseIf (ECU.AccessLevel = ECU.ECUAccessLevel.CANOnlyLocked) Then '  CANOnly locked ECU (no memory reading possible)
                CBCAN50.Enabled = False
                BDownloadECU.Enabled = False
                CBCAN80.Enabled = True
                CBCANOBD.Enabled = True
                CBTimerQuery.Enabled = True
            ElseIf (ECU.AccessLevel = ECU.ECUAccessLevel.CANOnlyUnlocked) Then '  CANOnly unlocked ECU
                CBCAN50.Enabled = True
                BDownloadECU.Enabled = True
                CBCAN80.Enabled = True
                CBCANOBD.Enabled = True
                CBTimerQuery.Enabled = True
            Else ' unknown ECU capabilities
                CBCAN50.Enabled = False
                BDownloadECU.Enabled = False
                CBCAN80.Enabled = False
                CBCANOBD.Enabled = False
                CBTimerQuery.Enabled = False
            End If
        Else
            ' ECU not connected
            CBCAN50.Enabled = False
            CBCAN80.Enabled = False
            CBCANOBD.Enabled = False
            CBTimerQuery.Enabled = False
            BDownloadECU.Enabled = False
        End If
    End Sub

    '****************************************
    '* manually trigger CAN 0x80 logger burst
    '****************************************
    Private Sub BBurst_Click(sender As Object, e As EventArgs) Handles BBurst.Click
        If (ECU.Adapter.isConnected) Then
            If (CBCAN80.Enabled) Then
                SimulateCAN80Burst()
            Else
                TextBox1.Text &= "Logger Burst not supported by ECU software" & vbCrLf
            End If

        Else
            TextBox1.Text &= "not connected" & vbCrLf
        End If
    End Sub

    '****************************************
    '* manually trigger CAN 0x50 memory read
    '****************************************
    Private Sub BMemoryRead_Click(sender As Object, e As EventArgs) Handles BMemoryRead.Click
        If (ECU.Adapter.isConnected) Then
            If (CBCAN50.Enabled) Then
                SimulateMemoryRead()
            Else
                TextBox1.Text &= "Memory Read not supported by ECU software" & vbCrLf
            End If

        Else
            TextBox1.Text &= "not connected" & vbCrLf
        End If
    End Sub

    '****************************************
    '* manually trigger OBD test
    '****************************************
    Private Sub BOBDRead_Click(sender As Object, e As EventArgs) Handles BOBDRead.Click
        If (ECU.Adapter.isConnected) Then
            If (CBCANOBD.Enabled) Then
                SimulateOBDRead()
            Else
                TextBox1.Text &= "OBD Read not supported by ECU software" & vbCrLf
            End If
        Else
            TextBox1.Text &= "not connected" & vbCrLf
        End If
    End Sub


    '*****************************************************
    '* Simulate CAN 0x80 message burst
    '*****************************************************
    Private Sub SimulateCAN80Burst()

        ECU.CAN80ProbeMaxID()
        Dim cmsg As New can2usbDLL.can2usb.CANMessage
        cmsg.id = &H80
        cmsg.len = 3
        Array.Resize(cmsg.data, cmsg.len)
        cmsg.data(0) = 8
        cmsg.data(1) = 1
        ECU.Adapter.SendAndWaitForCANMessageID(cmsg, ECU.CAN80MaxID)
        AddToTBByID(1, ECU.CAN80MaxID)       ' &H2B4 is added to textbox if we catch it
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

        'PrintCANMessageBuffers()
        b = ECU.ECUQueryOBD(&H22, &H211)
        If (b IsNot Nothing) Then
            TextBox1.Text &= System.Text.Encoding.ASCII.GetString(b) & vbCrLf
        Else
            TextBox1.Text &= "no reply to OBD query" & vbCrLf
        End If
        '       PrintCANMessageBuffers()
        b = ECU.ECUQueryOBD(&H9, &H4)
        '        PrintCANMessageBuffers()
        If (b IsNot Nothing) Then
            TextBox1.Text &= System.Text.Encoding.ASCII.GetString(b) & vbCrLf
        Else
            TextBox1.Text &= "no reply to OBD query" & vbCrLf
        End If
    End Sub

    '*****************************************************
    '* Dump CANMessage Buffer
    '*****************************************************
    Private Sub PrintCANMessageBuffers()
        Dim cb() As can2usbDLL.can2usb.CANMessage
        cb = ECU.Adapter.GetCANMessagesBuffer

        For i As Integer = 0 To cb.Length - 1
            TextBox1.Text &= "(" & i & ") ID: 0x" & Hex(cb(i).id)
            For j As Integer = 0 To cb(i).len - 1
                TextBox1.Text &= " 0x" & Hex(cb(i).data(j))
            Next
            TextBox1.Text &= vbCrLf
        Next
        If (cb.Length = 0) Then
            TextBox1.Text &= "<CANMessages empty>" & vbCrLf
        End If
    End Sub

    Private Sub AddToTBByID(ByVal addr, ByVal id)
        Dim cb() As can2usbDLL.can2usb.CANMessage
        cb = ECU.Adapter.GetCANMessagesBuffer()
        For i = 0 To cb.Length - 1
            Dim data As String = ""
            For j As Integer = 0 To cb(i).data.Length - 1
                data &= " 0x" & Hex(cb(i).data(j))
            Next
            'TextBox1.Text &= addr & " = " & Hex(cb(i).id) & data & vbCrLf
            TextBox1.Text &= Hex(cb(i).id) & vbCrLf
        Next
    End Sub

    '*****************************************************
    '* Timer Event, based on selected tests do some action
    '*****************************************************
    Private Sub TimerQuery_Tick(sender As Object, e As EventArgs) Handles TimerQuery.Tick
        If (CBTimerQuery.Checked And ECU.Adapter.isConnected) Then
            If (CBCAN50.Checked) Then
                SimulateMemoryRead()
            End If
            If (CBCAN80.Checked) Then
                SimulateCAN80Burst()
            End If
            If (CBCANOBD.Checked) Then
                SimulateOBDRead()
            End If
            If (CBKLINEOBD.Checked) Then
                SimulateKLINEOBDRead(&H9, &H2)
            End If
        End If
    End Sub

    Private Sub SBPollInterval_Scroll(sender As Object, e As ScrollEventArgs) Handles SBPollInterval.Scroll
        TimerQuery.Interval = SBPollInterval.Value
        LPollInterval.Text = SBPollInterval.Value & " ms"
    End Sub



    '*****************************************************
    '* Download various memory ranges from the ECU
    '*****************************************************
    Private Sub BDownloadECU_Click(sender As Object, e As EventArgs) Handles BDownloadECU.Click
        FolderBrowserDialog_download.RootFolder = Environment.SpecialFolder.MyDocuments
        FolderBrowserDialog_download.Description = "Select directory to dump ECU download"
        If FolderBrowserDialog_download.ShowDialog() = Windows.Forms.DialogResult.OK Then
            Dim binpath = FolderBrowserDialog_download.SelectedPath
            Dim d_bootldr As New Downloader(&H0, &H10000)
            If (d_bootldr.ShowDialog() = DialogResult.Abort) Then
                d_bootldr.Dispose()
                MessageBox.Show("Error while downloading Bootloader (0x0-0xFFFF)", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                Exit Sub
            End If
            SaveBINFile(binpath & "\bootldr.bin", d_bootldr.bytes)

            Dim d_calrom As New Downloader(&H10000, &H10000)
            If (d_calrom.ShowDialog() = DialogResult.Abort) Then
                d_calrom.Dispose()
                MessageBox.Show("Error while downloading CALROM (0x10000-0x1FFFF)", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                Exit Sub
            End If
            SaveBINFile(binpath & "\calrom.bin", d_calrom.bytes)

            Dim d_prog As New Downloader(&H20000, &H60000)
            If (d_prog.ShowDialog() = DialogResult.Abort) Then
                d_prog.Dispose()
                MessageBox.Show("Error while downloading PROGRAM (0x20000-0x7FFFF)", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                Exit Sub
            End If
            SaveBINFile(binpath & "\prog.bin", d_prog.bytes)

            Dim d_decram As New Downloader(&H2F8000, &H800)
            If (d_decram.ShowDialog() = DialogResult.Abort) Then
                d_decram.Dispose()
                MessageBox.Show("Error while downloading DECRAM (0x2F8000-0x2F87FF)", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                Exit Sub
            End If

            SaveBINFile(binpath & "\decram.bin", d_decram.bytes)

            ' download 0x10000 bytes from CALRAM (0x3f8000) - these are the bytes used for the maps when the ECU is running (live mode)
            Dim d_calram As New Downloader(&H3F8000, &H8000)
            If (d_calram.ShowDialog() = DialogResult.Abort) Then
                d_calram.Dispose()
                MessageBox.Show("Error while downloading CALRAM (0x3F8000-0x3FFFFF)", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                Exit Sub
            End If
            SaveBINFile(binpath & "\calram.bin", d_calram.bytes)
        End If
    End Sub

    Public Function SaveBINFile(ByVal strFilename As String, ByVal bytesToWrite() As Byte) As Boolean
        Dim hash = SHA1.Create
        Dim hashvalue() As Byte

        Using fsNew As FileStream = New FileStream(strFilename, FileMode.Create, FileAccess.Write)
            TextBox1.Text &= "Writing " & strFilename & "(0x" & Hex(bytesToWrite.Length) & ")" & vbCrLf
            fsNew.Write(bytesToWrite, 0, bytesToWrite.Length)
            fsNew.Close()
            Dim fsHash As FileStream = File.OpenRead(strFilename)
            fsHash.Position = 0
            hashvalue = hash.ComputeHash(fsHash)
            fsHash.Close()
            Dim hash_hex = PrintByteArray(hashvalue)
            TextBox1.Text &= "sha1sum: " & hash_hex & vbCrLf
        End Using
        Return True
    End Function

    Public Function PrintByteArray(ByVal array() As Byte)
        Dim hex_value As String = ""
        ' We traverse the array of bytes
        Dim i As Integer
        For i = 0 To array.Length - 1

            ' We convert each byte in hexadecimal
            hex_value += array(i).ToString("X2")

        Next i
        ' We return the string in lowercase
        Return hex_value.ToLower
    End Function

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        ECU.Adapter.SendGetVersion()
        ECU.Adapter.GetVersion()
        ECU.Adapter.SendKLINEInit()
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        SimulateKLINEOBDRead(&H9, &H2)
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        SimulateKLINEOBDRead(&H1, &HD)
    End Sub

    Public Sub SimulateKLINEOBDRead(ByVal Mode As Integer, ByVal Pid As Integer)
        Dim kmsg As New can2usbDLL.can2usb.KLINEMessage
        Dim data_str As String = ""

        kmsg.len = 5
        Array.Resize(kmsg.data, kmsg.len)
        kmsg.data(0) = &H68
        kmsg.data(1) = &H6A
        kmsg.data(2) = &HF1         ' TEST ID (SOURCE)
        kmsg.data(3) = Mode          ' MODE
        kmsg.data(4) = Pid          ' PID
        ECU.Adapter.SendKLINEMessage(kmsg)
        Dim kb() As can2usbDLL.can2usb.KLINEMessage

        kb = ECU.Adapter.GetKLINEMessagesBuffer()
        For i As Integer = 0 To kb.Length - 1
            TextBox1.Text &= "Buffer: " & i & " "
            For j As Integer = 0 To kb(i).len - 1
                TextBox1.Text &= " 0x" & Hex(kb(i).data(j))
            Next j
            If (kb(i).len = 10) Then
                For j As Integer = 6 To 9
                    If (kb(i).data(j) <> 0) Then
                        data_str &= Chr(kb(i).data(j))
                    End If
                Next j
            End If
            TextBox1.Text &= " (0x" & Hex(kb(i).crc) & ")"  ' crc byte
            TextBox1.Text &= vbCrLf
        Next i
        If (data_str <> "") Then
            TextBox1.Text &= "Text: " & data_str & vbCrLf
        End If
    End Sub


End Class
