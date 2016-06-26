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
            b = ECU.ECUReadMemory(a, stepsize)
            retrycounter = 0
            While (b Is Nothing) And (retrycounter < max_retries)
                b = ECU.ECUReadMemory(a, stepsize)
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
            b = ECU.ECUReadMemory(a, remainder)
            retrycounter = 0
            While (b Is Nothing) And (retrycounter < max_retries)
                b = ECU.ECUReadMemory(a, remainder)
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

    Private Sub Downloader_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub
End Class