'Adapted from ShadowMud.NET - Open Source Mud Server Framework
'
'Copyright (C) 2001-2004 
'   Tim Davis (darkmercenary@earthlink.net)

'This program is free software; you can redistribute it and/or modify it under
' the terms of the GNU General Public License as published by the Free Software
'Foundation; either version 2 of the License, or (at your option) any later version.

'This program is distributed in the hope that it will be useful, but WITHOUT ANY
'WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A 
'PARTICULAR PURPOSE. See the GNU General Public License for more details.

'You should have received a copy of the GNU General Public License along with this
'program; if not, write to the Free Software Foundation, Inc., 59 Temple Place, 
'Suite 330, Boston, MA 02111-1307 USA

Imports System
Imports System.Text
Imports System.Net
Imports System.Net.Sockets

Public Class StateObject

    Public WorkSocket As Socket = Nothing
    Public BufferSize As Integer = 32768
    Public Buffer(BufferSize) As Byte
    'Public StrBuilder As New StringBuilder

End Class

Public Class AsyncSocket

    Private m_SocketID As String
    Private m_tmpSocket As Socket
    Private m_recBuffer As String
    Public IsOpen As Boolean = False
    Public MoreClean As Boolean = False
    Public Event socketDisconnected(ByVal SocketID As String)
    Public Event socketDataArrival(ByVal SocketID As String, ByVal SocketData As Byte(), ByVal DataLen As Integer)
    'Public Event socketConnected(ByVal SocketID As String)

    Public Sub New()

        m_tmpSocket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

    End Sub

    Public Sub Close()

        m_tmpSocket.Shutdown(SocketShutdown.Both)
        m_tmpSocket.Close()

    End Sub

    Public Sub Connect(ByVal hostIP As String, ByVal hostPort As Integer)

        Dim hostEndPoint As New IPEndPoint(IPAddress.Parse(hostIP), hostPort)
        'Dim obj_Socket As New Socket(hostEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        'm_tmpSocket = New Socket(hostEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        Dim obj_Socket As Socket = m_tmpSocket

        Try
            obj_Socket.BeginConnect(hostEndPoint, New AsyncCallback(AddressOf onConnectionComplete), obj_Socket)
        Catch ex As Exception
            MsgBox("AsyncSocket:  Connect() error " & ex.Message)
        End Try
    End Sub

    Private Sub onConnectionComplete(ByVal ar As IAsyncResult)

        m_tmpSocket = CType(ar.AsyncState, Socket)
        m_tmpSocket.EndConnect(ar)
        'RaiseEvent socketConnected("null")
        IsOpen = True
        Dim obj_Socket As Socket = m_tmpSocket
        Dim obj_SocketState As New StateObject
        obj_SocketState.WorkSocket = obj_Socket
        obj_Socket.BeginReceive(obj_SocketState.Buffer, 0, obj_SocketState.BufferSize, 0, New AsyncCallback(AddressOf onDataArrival), obj_SocketState)
    End Sub

    Private Sub onDataArrival(ByVal ar As IAsyncResult)

        Try

            Dim obj_SocketState As StateObject = CType(ar.AsyncState, StateObject)
            Dim obj_Socket As Socket = obj_SocketState.WorkSocket
            'Dim sck_Data As String
            Dim BytesRead As Integer = obj_Socket.EndReceive(ar)
            If BytesRead > 0 Then
                RaiseEvent socketDataArrival(m_SocketID, obj_SocketState.Buffer, BytesRead)
            End If
            'Start recieving again
            obj_Socket.BeginReceive(obj_SocketState.Buffer, 0, obj_SocketState.BufferSize, 0, New AsyncCallback(AddressOf onDataArrival), obj_SocketState)

        Catch e As Exception

            RaiseEvent socketDisconnected(m_SocketID)

        End Try

    End Sub

    Public Sub Send(ByVal tmp_Data As String)

        Try

            Dim obj_StateObject As New StateObject
            obj_StateObject.WorkSocket = m_tmpSocket
            Dim Buffer As Byte() = Encoding.ASCII.GetBytes(tmp_Data)
            m_tmpSocket.BeginSend(Buffer, 0, Buffer.Length, 0, New AsyncCallback(AddressOf onSendComplete), obj_StateObject)

        Catch ex As Exception

            MsgBox("AsyncSocket:  Send() error " & ex.Message)

        End Try

    End Sub

    Private Sub onSendComplete(ByVal ar As IAsyncResult)

        Dim obj_SocketState As StateObject = CType(ar.AsyncState, StateObject)
        Dim obj_Socket As Socket = obj_SocketState.WorkSocket
        Dim BytesSent As Integer = obj_Socket.EndSend(ar)

    End Sub

End Class