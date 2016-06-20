<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
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
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.TextBox1 = New System.Windows.Forms.TextBox()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.Button3 = New System.Windows.Forms.Button()
        Me.BBurst = New System.Windows.Forms.Button()
        Me.TimerStats = New System.Windows.Forms.Timer(Me.components)
        Me.GroupBox1 = New System.Windows.Forms.GroupBox()
        Me.TBMemoryReadCounter = New System.Windows.Forms.TextBox()
        Me.Label9 = New System.Windows.Forms.Label()
        Me.TBBurstCount = New System.Windows.Forms.TextBox()
        Me.Label8 = New System.Windows.Forms.Label()
        Me.Label7 = New System.Windows.Forms.Label()
        Me.TBCANMessagesIdx = New System.Windows.Forms.TextBox()
        Me.TBTriggerHit = New System.Windows.Forms.TextBox()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.TBRXWaitForIDTimeouts = New System.Windows.Forms.TextBox()
        Me.TBRXShortMsgErrors = New System.Windows.Forms.TextBox()
        Me.TBRXCRCErrors = New System.Windows.Forms.TextBox()
        Me.TBRXValidReceived = New System.Windows.Forms.TextBox()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.TBMessagesSent = New System.Windows.Forms.TextBox()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.GroupBox2 = New System.Windows.Forms.GroupBox()
        Me.CBCANOBD = New System.Windows.Forms.CheckBox()
        Me.LPollInterval = New System.Windows.Forms.Label()
        Me.SBPollInterval = New System.Windows.Forms.HScrollBar()
        Me.CBTimerQuery = New System.Windows.Forms.CheckBox()
        Me.CBCAN50 = New System.Windows.Forms.CheckBox()
        Me.CBCAN80 = New System.Windows.Forms.CheckBox()
        Me.BMemoryRead = New System.Windows.Forms.Button()
        Me.TimerQuery = New System.Windows.Forms.Timer(Me.components)
        Me.BOBDRead = New System.Windows.Forms.Button()
        Me.GroupBox1.SuspendLayout()
        Me.GroupBox2.SuspendLayout()
        Me.SuspendLayout()
        '
        'TextBox1
        '
        Me.TextBox1.Location = New System.Drawing.Point(12, 242)
        Me.TextBox1.Multiline = True
        Me.TextBox1.Name = "TextBox1"
        Me.TextBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical
        Me.TextBox1.Size = New System.Drawing.Size(582, 223)
        Me.TextBox1.TabIndex = 0
        '
        'Button1
        '
        Me.Button1.Location = New System.Drawing.Point(6, 19)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(75, 23)
        Me.Button1.TabIndex = 1
        Me.Button1.Text = "Connect"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'Button2
        '
        Me.Button2.Location = New System.Drawing.Point(87, 19)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(75, 23)
        Me.Button2.TabIndex = 2
        Me.Button2.Text = "Disconnect"
        Me.Button2.UseVisualStyleBackColor = True
        '
        'Button3
        '
        Me.Button3.Location = New System.Drawing.Point(515, 213)
        Me.Button3.Name = "Button3"
        Me.Button3.Size = New System.Drawing.Size(75, 23)
        Me.Button3.TabIndex = 3
        Me.Button3.Text = "Clear"
        Me.Button3.UseVisualStyleBackColor = True
        '
        'BBurst
        '
        Me.BBurst.Location = New System.Drawing.Point(519, 106)
        Me.BBurst.Name = "BBurst"
        Me.BBurst.Size = New System.Drawing.Size(75, 23)
        Me.BBurst.TabIndex = 4
        Me.BBurst.Text = "Burst 0x80"
        Me.BBurst.UseVisualStyleBackColor = True
        '
        'TimerStats
        '
        Me.TimerStats.Enabled = True
        Me.TimerStats.Interval = 250
        '
        'GroupBox1
        '
        Me.GroupBox1.Controls.Add(Me.TBMemoryReadCounter)
        Me.GroupBox1.Controls.Add(Me.Label9)
        Me.GroupBox1.Controls.Add(Me.TBBurstCount)
        Me.GroupBox1.Controls.Add(Me.Label8)
        Me.GroupBox1.Controls.Add(Me.Label7)
        Me.GroupBox1.Controls.Add(Me.TBCANMessagesIdx)
        Me.GroupBox1.Controls.Add(Me.TBTriggerHit)
        Me.GroupBox1.Controls.Add(Me.Label6)
        Me.GroupBox1.Controls.Add(Me.TBRXWaitForIDTimeouts)
        Me.GroupBox1.Controls.Add(Me.TBRXShortMsgErrors)
        Me.GroupBox1.Controls.Add(Me.TBRXCRCErrors)
        Me.GroupBox1.Controls.Add(Me.TBRXValidReceived)
        Me.GroupBox1.Controls.Add(Me.Label5)
        Me.GroupBox1.Controls.Add(Me.Label4)
        Me.GroupBox1.Controls.Add(Me.TBMessagesSent)
        Me.GroupBox1.Controls.Add(Me.Label3)
        Me.GroupBox1.Controls.Add(Me.Label2)
        Me.GroupBox1.Controls.Add(Me.Label1)
        Me.GroupBox1.Location = New System.Drawing.Point(12, 106)
        Me.GroupBox1.Name = "GroupBox1"
        Me.GroupBox1.Size = New System.Drawing.Size(437, 130)
        Me.GroupBox1.TabIndex = 9
        Me.GroupBox1.TabStop = False
        Me.GroupBox1.Text = "Counters"
        '
        'TBMemoryReadCounter
        '
        Me.TBMemoryReadCounter.Location = New System.Drawing.Point(325, 81)
        Me.TBMemoryReadCounter.Name = "TBMemoryReadCounter"
        Me.TBMemoryReadCounter.Size = New System.Drawing.Size(100, 20)
        Me.TBMemoryReadCounter.TabIndex = 16
        '
        'Label9
        '
        Me.Label9.AutoSize = True
        Me.Label9.Location = New System.Drawing.Point(217, 84)
        Me.Label9.Name = "Label9"
        Me.Label9.Size = New System.Drawing.Size(107, 13)
        Me.Label9.TabIndex = 15
        Me.Label9.Text = "Memory Read Count:"
        '
        'TBBurstCount
        '
        Me.TBBurstCount.Location = New System.Drawing.Point(111, 103)
        Me.TBBurstCount.Name = "TBBurstCount"
        Me.TBBurstCount.Size = New System.Drawing.Size(100, 20)
        Me.TBBurstCount.TabIndex = 14
        '
        'Label8
        '
        Me.Label8.AutoSize = True
        Me.Label8.Location = New System.Drawing.Point(3, 106)
        Me.Label8.Name = "Label8"
        Me.Label8.Size = New System.Drawing.Size(65, 13)
        Me.Label8.TabIndex = 13
        Me.Label8.Text = "Burst Count:"
        '
        'Label7
        '
        Me.Label7.AutoSize = True
        Me.Label7.Location = New System.Drawing.Point(3, 84)
        Me.Label7.Name = "Label7"
        Me.Label7.Size = New System.Drawing.Size(100, 13)
        Me.Label7.TabIndex = 12
        Me.Label7.Text = "CAN Messages Idx:"
        '
        'TBCANMessagesIdx
        '
        Me.TBCANMessagesIdx.Location = New System.Drawing.Point(111, 81)
        Me.TBCANMessagesIdx.Name = "TBCANMessagesIdx"
        Me.TBCANMessagesIdx.Size = New System.Drawing.Size(100, 20)
        Me.TBCANMessagesIdx.TabIndex = 11
        '
        'TBTriggerHit
        '
        Me.TBTriggerHit.Location = New System.Drawing.Point(111, 58)
        Me.TBTriggerHit.Name = "TBTriggerHit"
        Me.TBTriggerHit.Size = New System.Drawing.Size(100, 20)
        Me.TBTriggerHit.TabIndex = 10
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Location = New System.Drawing.Point(3, 61)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(59, 13)
        Me.Label6.TabIndex = 9
        Me.Label6.Text = "Trigger Hit:"
        '
        'TBRXWaitForIDTimeouts
        '
        Me.TBRXWaitForIDTimeouts.Location = New System.Drawing.Point(326, 58)
        Me.TBRXWaitForIDTimeouts.Name = "TBRXWaitForIDTimeouts"
        Me.TBRXWaitForIDTimeouts.Size = New System.Drawing.Size(100, 20)
        Me.TBRXWaitForIDTimeouts.TabIndex = 8
        '
        'TBRXShortMsgErrors
        '
        Me.TBRXShortMsgErrors.Location = New System.Drawing.Point(326, 35)
        Me.TBRXShortMsgErrors.Name = "TBRXShortMsgErrors"
        Me.TBRXShortMsgErrors.Size = New System.Drawing.Size(100, 20)
        Me.TBRXShortMsgErrors.TabIndex = 7
        '
        'TBRXCRCErrors
        '
        Me.TBRXCRCErrors.Location = New System.Drawing.Point(326, 13)
        Me.TBRXCRCErrors.Name = "TBRXCRCErrors"
        Me.TBRXCRCErrors.Size = New System.Drawing.Size(100, 20)
        Me.TBRXCRCErrors.TabIndex = 6
        '
        'TBRXValidReceived
        '
        Me.TBRXValidReceived.Location = New System.Drawing.Point(111, 35)
        Me.TBRXValidReceived.Name = "TBRXValidReceived"
        Me.TBRXValidReceived.Size = New System.Drawing.Size(100, 20)
        Me.TBRXValidReceived.TabIndex = 6
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(231, 61)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(77, 13)
        Me.Label5.TabIndex = 4
        Me.Label5.Text = "Timeouts (RX):"
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(231, 38)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(89, 13)
        Me.Label4.TabIndex = 3
        Me.Label4.Text = "Short Errors (RX):"
        '
        'TBMessagesSent
        '
        Me.TBMessagesSent.Location = New System.Drawing.Point(111, 13)
        Me.TBMessagesSent.Name = "TBMessagesSent"
        Me.TBMessagesSent.Size = New System.Drawing.Size(100, 20)
        Me.TBMessagesSent.TabIndex = 5
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(231, 16)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(86, 13)
        Me.Label3.TabIndex = 2
        Me.Label3.Text = "CRC Errors (RX):"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(3, 38)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(102, 13)
        Me.Label2.TabIndex = 1
        Me.Label2.Text = "Messages Valid RX:"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(3, 16)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(75, 13)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "Messages TX:"
        '
        'GroupBox2
        '
        Me.GroupBox2.Controls.Add(Me.CBCANOBD)
        Me.GroupBox2.Controls.Add(Me.LPollInterval)
        Me.GroupBox2.Controls.Add(Me.SBPollInterval)
        Me.GroupBox2.Controls.Add(Me.CBTimerQuery)
        Me.GroupBox2.Controls.Add(Me.CBCAN50)
        Me.GroupBox2.Controls.Add(Me.Button2)
        Me.GroupBox2.Controls.Add(Me.CBCAN80)
        Me.GroupBox2.Controls.Add(Me.Button1)
        Me.GroupBox2.Location = New System.Drawing.Point(12, 12)
        Me.GroupBox2.Name = "GroupBox2"
        Me.GroupBox2.Size = New System.Drawing.Size(582, 88)
        Me.GroupBox2.TabIndex = 10
        Me.GroupBox2.TabStop = False
        Me.GroupBox2.Text = "Setup"
        '
        'CBCANOBD
        '
        Me.CBCANOBD.AutoSize = True
        Me.CBCANOBD.Location = New System.Drawing.Point(469, 65)
        Me.CBCANOBD.Name = "CBCANOBD"
        Me.CBCANOBD.Size = New System.Drawing.Size(109, 17)
        Me.CBCANOBD.TabIndex = 5
        Me.CBCANOBD.Text = "CAN OBD (Read)"
        Me.CBCANOBD.UseVisualStyleBackColor = True
        '
        'LPollInterval
        '
        Me.LPollInterval.AutoSize = True
        Me.LPollInterval.Location = New System.Drawing.Point(343, 19)
        Me.LPollInterval.Name = "LPollInterval"
        Me.LPollInterval.Size = New System.Drawing.Size(65, 13)
        Me.LPollInterval.TabIndex = 4
        Me.LPollInterval.Text = "LPollInterval"
        '
        'SBPollInterval
        '
        Me.SBPollInterval.Location = New System.Drawing.Point(313, 45)
        Me.SBPollInterval.Maximum = 1000
        Me.SBPollInterval.Minimum = 10
        Me.SBPollInterval.Name = "SBPollInterval"
        Me.SBPollInterval.Size = New System.Drawing.Size(138, 14)
        Me.SBPollInterval.TabIndex = 3
        Me.SBPollInterval.Value = 100
        '
        'CBTimerQuery
        '
        Me.CBTimerQuery.AutoSize = True
        Me.CBTimerQuery.Location = New System.Drawing.Point(336, 62)
        Me.CBTimerQuery.Name = "CBTimerQuery"
        Me.CBTimerQuery.Size = New System.Drawing.Size(90, 17)
        Me.CBTimerQuery.TabIndex = 2
        Me.CBTimerQuery.Text = "Polling Active"
        Me.CBTimerQuery.UseVisualStyleBackColor = True
        '
        'CBCAN50
        '
        Me.CBCAN50.AutoSize = True
        Me.CBCAN50.Location = New System.Drawing.Point(469, 42)
        Me.CBCAN50.Name = "CBCAN50"
        Me.CBCAN50.Size = New System.Drawing.Size(109, 17)
        Me.CBCAN50.TabIndex = 1
        Me.CBCAN50.Text = "CAN 0x50 (Read)"
        Me.CBCAN50.UseVisualStyleBackColor = True
        '
        'CBCAN80
        '
        Me.CBCAN80.AutoSize = True
        Me.CBCAN80.Location = New System.Drawing.Point(469, 19)
        Me.CBCAN80.Name = "CBCAN80"
        Me.CBCAN80.Size = New System.Drawing.Size(107, 17)
        Me.CBCAN80.TabIndex = 0
        Me.CBCAN80.Text = "CAN 0x80 (Burst)"
        Me.CBCAN80.UseVisualStyleBackColor = True
        '
        'BMemoryRead
        '
        Me.BMemoryRead.Location = New System.Drawing.Point(482, 135)
        Me.BMemoryRead.Name = "BMemoryRead"
        Me.BMemoryRead.Size = New System.Drawing.Size(112, 23)
        Me.BMemoryRead.TabIndex = 11
        Me.BMemoryRead.Text = "Memory Read 0x50"
        Me.BMemoryRead.UseVisualStyleBackColor = True
        '
        'TimerQuery
        '
        Me.TimerQuery.Enabled = True
        '
        'BOBDRead
        '
        Me.BOBDRead.Location = New System.Drawing.Point(482, 164)
        Me.BOBDRead.Name = "BOBDRead"
        Me.BOBDRead.Size = New System.Drawing.Size(112, 23)
        Me.BOBDRead.TabIndex = 12
        Me.BOBDRead.Text = "OBD Read"
        Me.BOBDRead.UseVisualStyleBackColor = True
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(606, 477)
        Me.Controls.Add(Me.BOBDRead)
        Me.Controls.Add(Me.BMemoryRead)
        Me.Controls.Add(Me.GroupBox2)
        Me.Controls.Add(Me.GroupBox1)
        Me.Controls.Add(Me.BBurst)
        Me.Controls.Add(Me.Button3)
        Me.Controls.Add(Me.TextBox1)
        Me.MaximizeBox = False
        Me.Name = "Form1"
        Me.Text = "can2usb Tester"
        Me.GroupBox1.ResumeLayout(False)
        Me.GroupBox1.PerformLayout()
        Me.GroupBox2.ResumeLayout(False)
        Me.GroupBox2.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents TextBox1 As TextBox
    Friend WithEvents Button1 As Button
    Friend WithEvents Button2 As Button
    Friend WithEvents Button3 As Button
    Friend WithEvents BBurst As Button
    Friend WithEvents TimerStats As Timer
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents TBRXWaitForIDTimeouts As TextBox
    Friend WithEvents TBRXShortMsgErrors As TextBox
    Friend WithEvents TBRXCRCErrors As TextBox
    Friend WithEvents TBRXValidReceived As TextBox
    Friend WithEvents Label5 As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents TBMessagesSent As TextBox
    Friend WithEvents TBTriggerHit As TextBox
    Friend WithEvents Label6 As Label
    Friend WithEvents GroupBox2 As GroupBox
    Friend WithEvents BMemoryRead As Button
    Friend WithEvents Label7 As Label
    Friend WithEvents TBCANMessagesIdx As TextBox
    Friend WithEvents TBBurstCount As TextBox
    Friend WithEvents Label8 As Label
    Friend WithEvents TBMemoryReadCounter As TextBox
    Friend WithEvents Label9 As Label
    Friend WithEvents CBTimerQuery As CheckBox
    Friend WithEvents CBCAN50 As CheckBox
    Friend WithEvents CBCAN80 As CheckBox
    Friend WithEvents TimerQuery As Timer
    Friend WithEvents LPollInterval As Label
    Friend WithEvents SBPollInterval As HScrollBar
    Friend WithEvents CBCANOBD As CheckBox
    Friend WithEvents BOBDRead As Button
End Class
