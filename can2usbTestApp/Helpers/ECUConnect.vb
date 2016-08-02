'*******************************************************************************
'* connection handling and capability checks for ECU
'* (c) Georg Swoboda 2016 <cn@warp.at>
'*******************************************************************************
Imports System.Management

Public Class ECUConnect

    ' ECU Access Levels
    Enum ECUAccessLevel
        Unknown = 0
        KLINE_CAN = 1
        CANOnlyLocked = 2
        CANOnlyUnlocked = 3
    End Enum

    Private CANSpeed As Integer = 0
    Private CANShieldType As Integer = 0
    Private CANReset As Boolean = False
    Private ComPortName As String = ""


    Private Sub BConnect_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BConnect.Click

        If (Not ECU.Adapter.isConnected) Then
            ''''''''''''''''''''''''''''''''''''''''''''''
            ' disconnect first
            ''''''''''''''''''''''''''''''''''''''''''''''
            LblAccessType.Text = ""
            LblCalibrationDetail.Text = ""
            DisconnectFromECU()
        End If
        ''''''''''''''''''''''''''''''''''''''''''''''
        ' connect to ECU
        ''''''''''''''''''''''''''''''''''''''''''''''
        Dim oComPortName As GenericListItem(Of String) = CType(CBComport.SelectedItem, GenericListItem(Of String))
        Try
            Me.ComPortName = oComPortName.Value.ToString
        Catch ex As Exception
            MessageBox.Show("You have not selected a COM Port", "Invalid COM Port", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Exit Sub
        End Try
        If Me.ComPortName <> "" Then
            ConnectToECU(Me.ComPortName, CANSpeed, CANShieldType)
            ReadCalibrationDetail()
            Form1.UpdateControls()
            'DisconnectFromECU()
        End If
    End Sub


    '**********************************************************
    '* Read Calibration version
    '* via memory read if possible
    '* otherwise via OBD
    '**********************************************************
    Private Sub ReadCalibrationDetail()
        Dim caldetail As String = ""
        Dim b() As Byte

        If (ECU.AccessLevel = ECU.ECUAccessLevel.CANOnlyUnlocked) Or (ECU.AccessLevel = ECU.ECUAccessLevel.KLINE_CAN) Then
            b = ECU.ECUReadMemory(&H10000, &H28)
            If (b IsNot Nothing) Then
                caldetail = System.Text.Encoding.ASCII.GetString(b)
                LblCalibrationDetail.Text = caldetail
            Else
                LblCalibrationDetail.Text = "unable to read"
            End If
        ElseIf ECU.AccessLevel = ECU.ECUAccessLevel.CANOnlyLocked Then
            b = ECU.ECUQueryOBD(&H9, &H4)
            If (b IsNot Nothing) Then
                caldetail &= System.Text.Encoding.ASCII.GetString(b)
                LblCalibrationDetail.Text = caldetail
            Else
                LblCalibrationDetail.Text = "unable to read"
            End If
        Else
            LblCalibrationDetail.Text = "unable to read"
        End If
    End Sub
    '
    ' close serial port, reset adapter version, and access level
    '
    Public Sub DisconnectFromECU()
        ECU.Adapter.Disconnect()
    End Sub

    '
    ' open serial port, query adapter version, probe access level
    '
    Public Sub ConnectToECU(ByVal ThisComPortName As String, ByVal speed As Integer, ByVal shield As Integer)
        ' reset form elements
        ECU.AccessLevel = ECUAccessLevel.Unknown
        UpdateAccessTypeLabel()
        TBAdapterVersion.Text = ""
        LblCalibrationDetail.Text = ""
        TBAdapterVersion.BackColor = Color.LightGray

        If ThisComPortName <> "" Then
            If (ECU.Adapter.Connect(ThisComPortName, CANReset) = True) Then
                Console.WriteLine("ConnectToECU() Speed = " & speed)
                Console.WriteLine("ConnectToECU() ShieldType = " & shield)
                ECU.Adapter.Init(speed, shield)
                Dim AdapterVersion = ECU.Adapter.GetVersion()
                If (AdapterVersion IsNot Nothing) Then
                    TBAdapterVersion.Text = AdapterVersion
                    If (ECUProbeLevels()) Then
                        TBAdapterVersion.BackColor = Color.LightGreen
                        ' save CAN speed setting in registry / speed is ok (we could read memory, or OBD)
                        Form1.T4eReg.SetECUCANSpeed(speed)
                        Form1.T4eReg.SetECUCANShieldType(shield)
                    End If
                    ' save comport setting in registry / adaper is ok (we could read version)                    
                    Form1.T4eReg.SetECUComPort(ThisComPortName)
                    Form1.T4eReg.SetECUCANReset(CANReset)
                End If
            Else
                Me.Show()
            End If
        Else
            MessageBox.Show("You have not selected a COM Port, or the stored COM Port is not available at this time." _
                            & vbCrLf & "Please make sure that the adapter is connected to this computer, and that the igntion is turned on",
                            "Invalid COM Port", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            ChkBECUAutoConnect.Checked = False
        End If
    End Sub
    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    ' Test Access Level to ECU 
    ' *) CAN/KLINE ECU
    ' *) CAN only ECU unlocked
    ' *) CAN only ECU locked
    ' return True if we are certain about the access level
    ' return False if we are not sure, or something failed that shouldnt fail
    ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Function ECUProbeLevels() As Boolean
        Dim b() As Byte

        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' Probe OBD 0x22, 0x211 - ECU Type 'T4E'
        ' only CAN ECUs can do that via CAN2USB if that fails assume KLINE_CAN    
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        b = ECU.ECUQueryOBD(&H22, &H211) ' we need a second call here, the first one gets ignored - fix later
        b = ECU.ECUQueryOBD(&H22, &H211) ' this should give us the ECU type (T4E)
        If (b Is Nothing) Then
            Console.WriteLine("ECUProbeLevels() - no reply to OBD, checking if its a KLINE ECU")
            ' no OBD so should be KLINE and free to read via CAN to verify this we read 0x10000
            ' reply doesnt matter as long as we get something (=! -1)            
            b = ECU.ECUReadMemory(&H10000, &H32)
            If (b IsNot Nothing) Then
                ' we got something, make sure its not just garbage
                If (b.Length = &H32) Then
                    ECU.AccessLevel = ECUAccessLevel.KLINE_CAN
                    UpdateAccessTypeLabel()
                    Return (True)
                End If
            End If
            ' nothing or nothing useful came back
            ECU.AccessLevel = ECUAccessLevel.Unknown
            UpdateAccessTypeLabel()
            Return (False)
        End If

        ' its not KLINE_CAN check if the ECU is unlocked by reading a well known
        ' address 0xA0C which should contain "T4E" as a string        
        b = ECU.ECUReadMemory(&HA0C, 16)
        If (b Is Nothing) Then
            ECU.AccessLevel = ECUAccessLevel.CANOnlyLocked
            UpdateAccessTypeLabel()
            Return (True)
        Else
            Console.WriteLine("ECUProbeLevels() - reply to read 0xA0C: " & System.Text.Encoding.ASCII.GetString(b))
            If System.Text.Encoding.ASCII.GetString(b).StartsWith("T4E") Then
                ECU.AccessLevel = ECUAccessLevel.CANOnlyUnlocked
                UpdateAccessTypeLabel()
                Return (True)
            End If
        End If

        ' catchall we never reach this hopefully
        ECU.AccessLevel = ECUAccessLevel.Unknown
        UpdateAccessTypeLabel()
        Return (False)
    End Function

    Private Sub ECUConnect_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ''''''''''''''''''''''''''''''
        ' initialize Form elements
        ''''''''''''''''''''''''''''''
        EnumerateComports()
        ' hide ComPort/TCPIP elements until we know which shield it is
        UpdateShieldControls()
        ' setup Shield Types
        CBShieldType.Items.Clear()
        CBShieldType.Items.Add(New GenericListItem(Of Integer)("SparkFun", can2usbDLL.can2usb.ShieldType.SparkFun))
        CBShieldType.Items.Add(New GenericListItem(Of Integer)("SeedStudio", can2usbDLL.can2usb.ShieldType.SeedStudio))
        CBShieldType.Items.Add(New GenericListItem(Of Integer)("PiCAN2", can2usbDLL.can2usb.ShieldType.PiCAN2))
        LoadSetup()
        LblAccessType.Text = ""
        LblCalibrationDetail.Text = ""
        ToolTip.SetToolTip(Me.RBSpeed500K, "500kBit CAN, used for MY08 and later T4e ECU's which only support CAN")
        ToolTip.SetToolTip(Me.RBSpeed1M, "1MBit CAN, used for MY07 and earlier T4e ECU's which also support K-LINE")
        ToolTip.SetToolTip(Me.BComportListReload, "re-enumerate all available COM Ports")
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    ' Load setup from saved registry values
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub LoadSetup()
        '''''''''''''''''''''''''''''''''''''''
        ' set CAN Speed to stored settings
        '''''''''''''''''''''''''''''''''''''''
        If Form1.T4eReg.GetECUCANSpeed() = 1000 Then
            RBSpeed1M.Checked = True
        End If
        If Form1.T4eReg.GetECUCANSpeed() = 500 Then
            RBSpeed500K.Checked = True
        End If
        If Form1.T4eReg.GetECUCANSpeed() = 0 Then
            RBSpeed500K.Checked = True
        End If
        '''''''''''''''''''''''''''''''''''''''
        ' set CAN Shield Type (what an ugly hack)
        '''''''''''''''''''''''''''''''''''''''
        CANShieldType = Form1.T4eReg.GetECUCANShieldType()
        ' now f*** around to set the combobox by "value"
        Dim idx = 0
        For Each member As GenericListItem(Of Integer) In CBShieldType.Items
            If member.Value = Form1.T4eReg.GetECUCANShieldType() Then
                CBShieldType.SelectedIndex = idx
                Exit For
            End If
            idx += 1
        Next
        UpdateShieldControls()

        '''''''''''''''''''''''''''''''''''''''
        ' set COM Port to stored settings
        '''''''''''''''''''''''''''''''''''''''
        Dim COMPort As String = Form1.T4eReg.GetECUComPort()
        CBComport.ValueMember = COMPort
        If (COMPort IsNot "") Then
            For i As Integer = 0 To CBComport.Items.Count - 1
                Dim oComPortName As GenericListItem(Of String) = CType(CBComport.Items.Item(i), GenericListItem(Of String))
                If oComPortName.Value.ToString = COMPort Then
                    CBComport.SelectedIndex = i
                    Me.ComPortName = oComPortName.Value.ToString
                    Exit For
                End If
            Next
        Else
            ' no com port name saved in registry yet
        End If
        ''''''''''''''''''''''''''''''''''''''''''''
        ' set IPAddress, TCPPort to stored settings
        ''''''''''''''''''''''''''''''''''''''''''''
        TBIpaddress.Text = Form1.T4eReg.GetECUIPAddress
        TBTCPPort.Text = 
        ''''''''''''''''''''''''''''''''''''''''''''''''''
        ' set Flag that tells to reset Arduino on connect
        ''''''''''''''''''''''''''''''''''''''''''''''''''
        CANReset = Form1.T4eReg.GetECUCANReset
        '''''''''''''''''''''''''''''''''''''''
        ' set Auto Connect to stored settings
        '''''''''''''''''''''''''''''''''''''''
        If Form1.T4eReg.GetECUAutoConnect Then
            ChkBECUAutoConnect.Checked = True
        End If
    End Sub
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    ' get list of COM ports and attached devices and populate CBComport
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub EnumerateComports()
        CBComport.Text = "<select COM Port>"
        CBComport.Items.Clear()
        Try
            Dim searcher As New Management.ManagementObjectSearcher("root\cimv2", "SELECT * FROM Win32_SerialPort")
            Dim name, comportname As String

            For Each queryObj As Management.ManagementObject In searcher.Get()
                name = queryObj("Name")
                comportname = queryObj("DeviceID")
                CBComport.Items.Add(New GenericListItem(Of String)(name, comportname))
            Next
        Catch ex As ManagementException
            MsgBox("Error while querying for WMI data (COM Ports): " & ex.Message)
        End Try
    End Sub

    Private Sub BComportListReload_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BComportListReload.Click
        EnumerateComports()
        LoadSetup()
    End Sub

    Private Sub ChkBECUAutoConnect_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ChkBECUAutoConnect.CheckedChanged
        Form1.T4eReg.SetECUAutoConnect(ChkBECUAutoConnect.Checked)
    End Sub

    Private Sub RBSpeed1M_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles RBSpeed1M.CheckedChanged
        CANSpeed = 1000
    End Sub

    Private Sub RBSpeed500K_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles RBSpeed500K.CheckedChanged
        CANSpeed = 500
    End Sub


    ' set text for LblAccessType according to ECU Access Capablities
    Private Sub UpdateAccessTypeLabel()
        If ECU.AccessLevel = ECUAccessLevel.CANOnlyLocked Then
            LblAccessType.Text = "CAN Bus restricted access"
        ElseIf ECU.AccessLevel = ECUAccessLevel.CANOnlyUnlocked Then
            LblAccessType.Text = "CAN Bus full access"
        ElseIf ECU.AccessLevel = ECUAccessLevel.KLINE_CAN Then
            LblAccessType.Text = "KLINE/CAN Bus full access"
        Else
            LblAccessType.Text = "no access"
        End If
    End Sub


    '*********************************************************************
    '* depending on the type of shield we need different control elements
    '*********************************************************************
    Sub UpdateShieldControls()
        If (CANShieldType = can2usbDLL.can2usb.ShieldType.PiCAN2) Then
            TBTCPPort.Enabled = True
            TBIpaddress.Enabled = True
            CBComport.Enabled = False
        Else
            TBTCPPort.Enabled = False
            TBIpaddress.Enabled = False
            CBComport.Enabled = True
        End If
    End Sub

    Public Sub New()

        ' This call is required by the Windows Form Designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
    End Sub

    Private Sub CBShieldType_SelectedValueChanged(sender As Object, e As EventArgs) Handles CBShieldType.SelectedValueChanged
        Dim val As GenericListItem(Of Integer) = CType(CBShieldType.SelectedItem, GenericListItem(Of Integer))
        CANShieldType = val.Value
        UpdateShieldControls()
    End Sub

    Private Sub CBReset_CheckedChanged(sender As Object, e As EventArgs) Handles CBReset.CheckedChanged
        CANReset = CBReset.Checked
    End Sub

End Class


