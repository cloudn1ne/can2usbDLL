'*******************************************************************************
'* wrapper class for ECU functions
'* (c) Georg Swoboda 2016 <cn@warp.at>
'*******************************************************************************

'#Const DBG_READMEMORY = 1
'#Const DBG_READOBD = 1

Public Class ECU
    Public Shared Adapter As New can2usbDLL.can2usb
    Public Shared AccessLevel As New ECUAccessLevel

    ' ECU Access Levels
    Enum ECUAccessLevel
        Unknown = 0
        KLINE_CAN = 1
        CANOnlyLocked = 2
        CANOnlyUnlocked = 3
    End Enum

    '*****************************************************
    '* Read ECU Memory of size (0xFF max) from addr
    '*****************************************************
    Public Function ECUReadMemory(ByVal addr As Integer, ByVal size As Byte) As Byte()
        Dim retval() As Byte = Nothing
        Dim len As Integer = 0
        Dim status As Boolean
        Dim a() As Byte = BitConverter.GetBytes(addr)

#If DBG_READMEMORY Then
        Console.WriteLine("ECUReadMemory() addr 0x" & Hex(addr))
        If (Not Adapter.isConnected) Then
            Console.WriteLine("ECUReadMemory() - adapter not connected")
            Return (retval)
        End If
#End If
        ' minimum read size is 8
        If (size < 8) Then
            size = 8
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
        status = ECU.Adapter.SendAndWaitForNumOfCANMessageIDs(cmsg, &H7A0, size / 8)
        'If (status) Then
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' process reply
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''' 
        ' allocate memory needed for fully reply
        Array.Resize(retval, size)
        Dim retval_idx As Integer = 0

        Dim cb() As can2usbDLL.can2usb.CANMessage
        cb = Adapter.GetCANMessagesBuffer
        For i As Integer = 0 To cb.Length - 1
            If (cb(i).id = &H7A0) Then
                If (retval_idx < size - cb(i).len) Then
                    Array.Copy(cb(i).data, 0, retval, retval_idx, cb(i).len)
                    retval_idx += cb(i).len
                End If
#If DBG_READMEMORY Then
                Console.Write(i & "*** 0x" & Hex(cb(i).id))
                For j As Integer = 0 To cb(i).len - 1
                    Console.Write(" " & Hex(cb(i).data(j)))
                Next
                Console.WriteLine("")
#End If
            Else
#If DBG_READMEMORY Then
                Console.Write(i & "    0x" & Hex(cb(i).id))
                For j As Integer = 0 To cb(i).len - 1
                    Console.Write(" " & Hex(cb(i).data(j)))
                Next
                Console.WriteLine("")
#End If
            End If
        Next
        'End If
        Return (retval)
    End Function


    '*****************************************************
    '* Read OBD Mode and Pid
    '*****************************************************
    Public Function ECUQueryOBD(ByVal Mode As Integer, ByVal Pid As Integer) As Byte()
        Dim retval() As Byte = Nothing
        Dim can_len As Integer = 0
        Dim obd_tx_len As Integer = 0
        Dim canline As String = ""

        Dim obd_cmd As String

        If (Not Adapter.isConnected) Then
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
        'Console.WriteLine("len: " & cmsg.len)
        'For i As Integer = 0 To cmsg.len - 1
        '   Console.WriteLine(Hex(cmsg.data(i)))
        'Next
        'Console.WriteLine(obd_cmd)
        ECU.Adapter.SendAndWaitForCANMessageID(cmsg, &H7E8, 1000)
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ' process reply
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''        
#If DBG_READOBD Then
        Dim cb() As can2usbDLL.can2usb.CANMessage
        cb = ECU.Adapter.GetCANMessagesBuffer
        For i As Integer = 0 To cb.Length - 1
            Console.WriteLine("OBD CAN buffer: " & Hex(cb(i).id))
            For j As Integer = 0 To cb(i).len - 1
                Console.WriteLine("(" & j & ") 0x" & Hex(cb(i).data(j)))
            Next
        Next
#End If

        cmsg = ECU.Adapter.GetFirstCANMessageBufferByID(&H7E8)
        If (cmsg.used = 0) Then
            Return (Nothing)
        End If

        Dim obd_rx_mode As Integer = cmsg.data(1)
        Dim obd_rx_pid As Integer = 0
        Dim obd_rx_len As Integer = cmsg.data(0)
        Dim obd_rx_datalen As Integer = obd_rx_len - obd_tx_len
        If (obd_tx_len = 2) Then
            obd_rx_pid = cmsg.data(2)
        ElseIf (obd_tx_len = 3) Then
            obd_rx_pid = cmsg.data(2) * 256 + cmsg.data(3)
        End If

#If DBG_READOBD Then
        Console.WriteLine("ECUQueryOBD() Reply Mode: 0x" & Hex(obd_rx_mode))
        Console.WriteLine("ECUQueryOBD() Reply PID: 0x" & Hex(obd_rx_pid))
        Console.WriteLine("ECUQueryOBD() Send Overall Size: 0x" & Hex(obd_tx_len))
        Console.WriteLine("ECUQueryOBD() Reply Data Size: 0x" & Hex(obd_rx_datalen))
#End If
        If (obd_rx_pid = Pid) And (obd_rx_mode - &H40 = Mode) Then
            ' successful OBD reply                    
            Array.Resize(retval, obd_rx_datalen)
            For i As Integer = 0 To obd_rx_datalen - 1
                retval(i) = cmsg.data(obd_tx_len + i + 1)
#If DBG_READOBD Then
                Console.WriteLine(" 0x" & Hex(retval(i)))
#End If
            Next
            Return (retval)
        End If
        Return (retval)
    End Function
End Class
