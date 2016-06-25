Public Class Downloader
    Private addr As Integer                  ' address of download
    Private len As Integer                   ' len of downloaded bytes
    Private max_retries As Integer = 50      ' maximum number of retries (TIMEOUTS) while downloading
    Public bytes() As Byte                   ' downloaded bytes

    Public Sub New(ByVal a As Integer, ByVal l As Integer)
        ' reserve some space for what we wanna download
        Me.addr = a
        Me.len = l
        Array.Resize(bytes, len)
        ' This call is required by the designer.
        InitializeComponent()
        ' Add any initialization after the InitializeComponent() call.
        Me.TopMost = True
    End Sub

    '*****************************************************
    '* Read ECU Memory of size (0xFF max) from addr
    '*****************************************************
    Public Function ECUReadMemory(ByVal addr As Integer, ByVal size As Byte) As Byte()
        Dim retval() As Byte = Nothing
        'Dim len As Integer = 0
        Dim status As Boolean
        Dim a() As Byte = BitConverter.GetBytes(addr)

        'Console.WriteLine("ECUReadMemory() 0x" & Hex(addr) & " len=" & size)
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
        'Console.WriteLine("Num Msgs :" & size / 8)
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
                If (cb(i).id = &H7A0) Then
                    Array.Copy(cb(i).data, 0, retval, retval_idx, cb(i).len)
                    'Console.WriteLine("RBytes:")
                    'For j As Integer = 0 To cb(i).len - 1
                    ' Console.Write(" 0x" & Hex(cb(i).data(j)))
                    ' Next
                    '         Console.WriteLine("")
                    retval_idx += cb(i).len
                End If
            Next
        End If
        Return (retval)
    End Function


    '*****************************************************
    '* Show Downloader popup
    '* download previously setup addr, len    
    '*****************************************************
    Private Sub Downloader_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        Application.DoEvents()
        PBDownload.Minimum = addr
        PBDownload.Maximum = addr + len
        LblDownloadStatus.Text = "Downloading ..."

        Dim stepsize As Integer
        Dim b() As Byte
        Dim bytes_idx As Integer = 0
        Dim retrycounter As Integer = 0

        ' maxium no. of bytes we can handle in one burst is 0xFF
        ' try to minimize the bursts by using larger chunks
        If (len < &HFF) Then
            stepsize = len
        Else
            stepsize = &HFF
        End If

        Dim chunks = len \ stepsize
        Dim c As Integer = 0
        Dim a As Integer

        'Console.WriteLine(vbCrLf & "Chunks: " & chunks & " of " & stepsize)
        '**********************************************************************
        '* download full chunks (0xFF)
        '**********************************************************************
        While (c < chunks)
            a = addr + (c * stepsize)
            LblDownloadStatus.Text = "Downloading 0x" & Hex(a)
            b = ECUReadMemory(a, stepsize)
            retrycounter = 0
            While (b Is Nothing) And (retrycounter < max_retries)
                b = ECUReadMemory(a, stepsize)
                retrycounter += 1
            End While
            If (retrycounter >= max_retries) Then
                ' indicate back to the calling form that something went wrong
                Me.DialogResult = Windows.Forms.DialogResult.Abort
                Me.Close()
                Exit Sub
            End If
            Array.Copy(b, 0, bytes, bytes_idx, b.Length)
            bytes_idx += b.Length
            PBDownload.Value = a
            LblDownloadStatus.Update()
            PBDownload.Update()
            c += 1 ' next chunk
        End While

        '**********************************************************************
        '* download any remaining bytes that dont fit in one full chunk (0xFF)
        '**********************************************************************
        Dim remainder As Integer = len Mod stepsize
        'Console.WriteLine("Rest: " & remainder)
        If (remainder > 0) Then
            a = addr + (chunks * stepsize)
            LblDownloadStatus.Text = "Downloading 0x" & Hex(a)
            Console.WriteLine(Hex(a) & " len =" & remainder)
            b = ECUReadMemory(a, remainder)
            retrycounter = 0
            While (b Is Nothing) And (retrycounter < max_retries)
                b = ECUReadMemory(a, remainder)
                retrycounter += 1
            End While
            If (retrycounter >= max_retries) Then
                ' indicate back to the calling form that something went wrong
                Me.DialogResult = Windows.Forms.DialogResult.Abort
                Me.Close()
                Exit Sub
            End If
            Array.Copy(b, 0, bytes, bytes_idx, remainder)
            'Console.WriteLine("Bytes:")
            'For i As Integer = 0 To b.Length - 1
            ' Console.WriteLine("0x" & Hex(b(i)))
            ' Next
            PBDownload.Value = a
            LblDownloadStatus.Update()
            PBDownload.Update()
        End If
        'Console.WriteLine("Total len: 0x" & Hex(bytes.Length))
        LblDownloadStatus.Text = "Downloading completed"
        Me.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub
End Class