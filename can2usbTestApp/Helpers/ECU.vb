'*******************************************************************************
'* wrapper class for ECU functions
'* (c) Georg Swoboda 2016 <cn@warp.at>
'*******************************************************************************


'#Const DBG_READOBD = 1
#Const DBG_MEMREAD = 1
Public Class ECU
    Public Shared Adapter As New can2usbDLL.can2usb
    Public Shared AccessLevel As New ECUAccessLevel
    Public Shared CAN80MaxID As Integer = 0

    ' ECU Access Levels
    Enum ECUAccessLevel
        Unknown = 0
        KLINE_CAN = 1
        CANOnlyLocked = 2
        CANOnlyUnlocked = 3
    End Enum

    '*****************************************************
    '* Try to determine the highest CAN 0x80 reply ID 
    '* for the connected ECU
    '*****************************************************
    Public Shared Sub CAN80ProbeMaxID()
        If (CAN80MaxID = 0) Then
            Dim cmsg As New can2usbDLL.can2usb.CANMessage
            cmsg.id = &H80
            cmsg.len = 3
            Array.Resize(cmsg.data, cmsg.len)
            cmsg.data(0) = 8
            cmsg.data(1) = 1
            ECU.Adapter.SendAndWaitForCANMessageID(cmsg, &H400) ' use 0x400 message to bail out from waiting
            Dim cb() As can2usbDLL.can2usb.CANMessage
            cb = ECU.Adapter.GetCANMessagesBuffer()
            Dim maxid = 0
            ' grab highest reply CAN ID that is in the range of &H200-&H3FF
            For i = 0 To cb.Length - 1
                If (cb(i).id > maxid) And (cb(i).id > &H200) And (cb(i).id < &H400) Then
                    maxid = cb(i).id
                End If
            Next
            If (maxid > 0) Then
                CAN80MaxID = maxid
                Console.WriteLine("CAN80MaxID: 0x" & Hex(maxid))
            End If
        End If
    End Sub

    '*****************************************************
    '* Read ECU Memory of size (0xFF max) from addr
    '*****************************************************
    Public Shared Function ECUReadMemory(ByVal addr As Integer, ByVal size As Byte) As Byte()
        Dim retval() As Byte = Nothing
        'Dim len As Integer = 0
        Dim status As Boolean
        Dim a() As Byte = BitConverter.GetBytes(addr)
#If DBG_MEMREAD Then
        Console.WriteLine("ECUReadMemory() 0x" & Hex(addr) & " len=" & size)
#End If
        If (Not ECU.Adapter.isConnected) Then
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
#If DBG_MEMREAD Then
        Console.WriteLine("Num Msgs :" & size / 8)
#End If
        If (size / 8 < 1) Then size = 8
        status = ECU.Adapter.SendAndWaitForNumOfCANMessageIDs(cmsg, &H7A0, size / 8)
        If (status) Then
            '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
            ' process reply
            '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''    
            ' allocate memory needed for fully reply (size)
            Array.Resize(retval, size)
            Dim retval_idx As Integer = 0

            Dim cb() As can2usbDLL.can2usb.CANMessage
            cb = ECU.Adapter.GetCANMessagesBuffer
            ' copy all the received cb.data() buffers to retval()
            For i As Integer = 0 To cb.Length - 1
                ' copy memread reply to buffer - make sure it fits
                If (cb(i).id = &H7A0) And (cb(i).data.Length <= size - retval_idx) Then
                    Array.Copy(cb(i).data, 0, retval, retval_idx, cb(i).len)
#If DBG_MEMREAD Then
                    Console.WriteLine("RBytes:")
                    For j As Integer = 0 To cb(i).len - 1
                        Console.Write(" 0x" & Hex(cb(i).data(j)))
                    Next
                    Console.WriteLine("")
#End If
                    retval_idx += cb(i).len
                End If
            Next
        End If
        Return (retval)
    End Function



    '*****************************************************
    '* Read OBD Mode and Pid
    '*****************************************************
    Public Shared Function ECUQueryOBD(ByVal Mode As Integer, ByVal Pid As Integer) As Byte()
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
#If DBG_READOBD Then
        Console.WriteLine("len: " & cmsg.len)
        For i As Integer = 0 To cmsg.len - 1
            Console.WriteLine(Hex(cmsg.data(i)))
        Next
        Console.WriteLine(obd_cmd)
#End If
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

        If (cmsg.data(0) <= 7) Then
            ' SINGLE FRAME RESPONSE 
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
        Else
            ' MULTIFRAME RESPONSE 
            Dim len_received = 0
            Dim multi_frame_len As Integer = (cmsg.data(0) And &HF) + cmsg.data(1) - 3 ' minus 3 bytes (mode ack, pid, 0x1)
            Array.Resize(retval, multi_frame_len)
#If DBG_READOBD Then
            Console.WriteLine("Multiframe len:" & multi_frame_len)
            Console.Write("First Frame: ")
            For i As Integer = 0 To cmsg.len - 1
                Console.Write(" 0x" & Hex(cmsg.data(i)))
            Next
            Console.WriteLine("")
#End If

            ' copy the last 3 bytes from the first frame into our buffer            
            Array.Copy(cmsg.data, cmsg.len - 3, retval, 0, 3)
            len_received = 3
            While (len_received < multi_frame_len)
                ' send flow control frame to ECU
                cmsg.id = &H7E0
                cmsg.data(0) = &H30     ' flow control 
                cmsg.data(1) = &H1      ' send us one frame at a time
                cmsg.data(2) = &H0      ' no delay
                ECU.Adapter.SendAndWaitForCANMessageID(cmsg, &H7E8, 1000)
                cmsg = ECU.Adapter.GetLastCANMessageBufferByID(&H7E8)
                If (cmsg.used = 0) Then
                    Return (Nothing)
                End If
#If DBG_READOBD Then
                Console.WriteLine("left len: " & multi_frame_len - len_received)
#End If
                Dim copy_len As Integer
                If (multi_frame_len - len_received >= 7) Then
                    copy_len = 7
                Else
                    copy_len = multi_frame_len - len_received
                End If
                Array.Copy(cmsg.data, 1, retval, len_received, copy_len)
                len_received += copy_len
#If DBG_READOBD Then
                Console.Write("Consecutive Frame: ")
                For i As Integer = 0 To cmsg.len - 1
                    Console.Write(" 0x" & Hex(cmsg.data(i)))
                Next
                Console.WriteLine("")
#End If
            End While
#If DBG_READOBD Then
            Console.Write("Retval: ")
            For i As Integer = 0 To retval.Length - 1
                Console.Write(" 0x" & Hex(retval(i)))
            Next
            Console.WriteLine("")
#End If
        End If

        Return (retval)
    End Function
End Class
