<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Downloader
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.GroupBox1 = New System.Windows.Forms.GroupBox()
        Me.LblDownloadStatus = New System.Windows.Forms.Label()
        Me.PBDownload = New System.Windows.Forms.ProgressBar()
        Me.GroupBox1.SuspendLayout()
        Me.SuspendLayout()
        '
        'GroupBox1
        '
        Me.GroupBox1.Controls.Add(Me.LblDownloadStatus)
        Me.GroupBox1.Controls.Add(Me.PBDownload)
        Me.GroupBox1.Location = New System.Drawing.Point(12, 12)
        Me.GroupBox1.Name = "GroupBox1"
        Me.GroupBox1.Size = New System.Drawing.Size(371, 68)
        Me.GroupBox1.TabIndex = 0
        Me.GroupBox1.TabStop = False
        Me.GroupBox1.Text = "Progress"
        '
        'LblDownloadStatus
        '
        Me.LblDownloadStatus.AutoSize = True
        Me.LblDownloadStatus.Location = New System.Drawing.Point(6, 52)
        Me.LblDownloadStatus.Name = "LblDownloadStatus"
        Me.LblDownloadStatus.Size = New System.Drawing.Size(99, 13)
        Me.LblDownloadStatus.TabIndex = 1
        Me.LblDownloadStatus.Text = "LblDownloadStatus"
        '
        'PBDownload
        '
        Me.PBDownload.Location = New System.Drawing.Point(3, 16)
        Me.PBDownload.Name = "PBDownload"
        Me.PBDownload.Size = New System.Drawing.Size(362, 23)
        Me.PBDownload.TabIndex = 0
        '
        'Downloader
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(395, 88)
        Me.ControlBox = False
        Me.Controls.Add(Me.GroupBox1)
        Me.Name = "Downloader"
        Me.Text = "Downloader"
        Me.GroupBox1.ResumeLayout(False)
        Me.GroupBox1.PerformLayout()
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents LblDownloadStatus As Label
    Friend WithEvents PBDownload As ProgressBar
End Class
