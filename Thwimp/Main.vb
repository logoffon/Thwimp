﻿'Thwimp - FOSS utility for ripping, viewing, and creating THP video files for Mario Kart Wii
'Copyright (C) 2018-2020 Tamkis

'This program is free software: you can redistribute it and/or modify
'it under the terms of the GNU General Public License as published by
'the Free Software Foundation, either version 3 of the License, or
'(at your option) any later version.

'This program is distributed in the hope that it will be useful,
'but WITHOUT ANY WARRANTY; without even the implied warranty of
'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'GNU General Public License for more details.

'You should have received a copy of the GNU General Public License
'along with this program.  If not, see <http://www.gnu.org/licenses/>.
'
'Email: tamkis[at]eaglesoftltd[dot]com

Imports System.IO
Imports System.Runtime.InteropServices

Public Class Main    
    'Global constants

    'Characters
    Shared strBAK As String = "\"                           'Backslash symbol
    Shared strQUOT As String = Chr(34)                      'Quote Symbol
    Shared strNL As String = Environment.NewLine            'Newline symbol

    Shared strPATH As String = Application.StartupPath      'Directory of the exe
    Const LISTING As String = "FileListing.txt"             'File containing the    file listing for BreakGOLD's image files
    Const CDESC As String = "FileCDesc.txt"                 '~                      description info for the control signal
    Const DESC As String = "FileDesc.txt"                   '~                      description info for the image files
    Const DATA As String = "FileData.txt"                   '~                      hard-coded image data for the image files

    'Exe utils used by app
    Shared strFMPackPath As String = ""                     'Path to FFMPEG exes (ffmpeg, ffplay)
    Const exeFMPeg As String = "ffmpeg.exe"                 'FFMPEG. Used for THP de/encoding
    Const exeFPlay As String = "ffplay.exe"                 'FFPlay. Used for viewing THP files

    'THP data
    Shared THPs(255) As THPData                           'Array containing all of the THPData

    ''' <summary>
    ''' Generic structure for image dims (width and height).
    ''' </summary>
    ''' <remarks>Like Vector2 in Unity3D</remarks>
    Structure Dims
        Dim width As UShort
        Dim height As UShort
    End Structure

    ''' <summary>
    ''' Structure for THP Video array info
    ''' </summary>
    ''' <remarks></remarks>
    Structure THPArr
        Dim row As Byte             'Amount of  rows
        Dim col As Byte             '~          cols
        Dim subV As Byte            '           subvideos (=rows*cols)
        Dim mult As Byte            '           Multiplicity for each cell
        Dim subVT As Byte           'Total amount of subvidoes (subvideos*multiplicity)
    End Structure

    ''' <summary>
    ''' Generic structure for frame info
    ''' </summary>
    ''' <remarks></remarks>
    Structure Frame
        Dim subframes As UShort     'Frames in a subvideo
        Dim totframes As UShort     'Total frames for THP file
    End Structure

    ''' <summary>
    ''' Audio info for THP files
    ''' </summary>
    ''' <remarks></remarks>
    Structure Audio
        Dim has As Boolean          'Does THP have audio stream?
        Dim Stereo As Boolean       'Is audio stereo?
        Dim freq As UShort          'Frequency of audio stream (Hz)
    End Structure

    ''' <summary>
    ''' Video info for THP files
    ''' </summary>
    ''' <remarks></remarks>
    Structure Video
        Dim TDim As Dims            'Total dimensions for THP file
        Dim THPinfo As THPArr       'THP array info
        Dim SDim As Dims            'Dimensions for subvideo
        Dim Frames As Frame         'Frames in video (sub/whole video)
        Dim Padding As Dims         'Dimenions of the padding (if any)
        Dim FPS As Single           'FPS of file
        Dim Ctrl As Boolean         'Does THP have a control signal in the padding?
        Dim CDesc As String         'Description of the ctrl signal
    End Structure

    ''' <summary>
    ''' Super-structure for representing THP info
    ''' </summary>
    ''' <remarks></remarks>
    Structure THPData
        Dim visual As Video                                 'Video data of THP
        Dim audial As Audio                                 'Audio data of THP
        Dim Desc As String                                  'Desc: Description of usage of file (in words)
        Dim File As String                                  'File: Directory of the file, relative to the THP root
        Dim Bad As Boolean                                  'Is this a dummy, bad THP entry?
    End Structure
    '========================

    'APP Setup code/THP Combo Box/Important THP struct stuff

    ''' <summary>
    ''' Application setup code onLoad
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub Main_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        'Initialize the application with some setup code

        'Disable form elements in THP tab (until the data in the "Options" tab is filled out)
        'Hide THPFile lable and combo box, THP Info Group box, THP Dec/Encoder boxes
        lblTHPFile.Visible = False
        cmbTHP.Visible = False
        grpTHPInfo.Visible = False
        grpTHPDec.Visible = False
        grpTHPEnc.Visible = False

        'Load the THP combo box data from the ext. files
        InitTHPData()
    End Sub

    ''' <summary>
    ''' Retrieve the hard-coded THP data from the four data files, dumps into RAM (THPs array)
    ''' </summary>
    ''' <remarks>Similar to InitIMGData in setup from BreakGold Editor; same purpose</remarks>
    Public Sub InitTHPData()
        Dim xFileData As StreamReader   'Streamreader object for reading the data from the files
        Dim strEntry As String          'String data read from the [File].ReadLine() method
        Dim bytItems As Byte            'Number of items in the parallel arrays

        Dim bytCtr1 As Byte             'Generic Counter variable #1
        Dim bytCtr2 As Byte             'Generic Counter variable #2

        'Variables for processing the DATA file
        Const SEP As String = ","       'Constant variable for the ASCII character separating the sub entries per line
        Const SEP2 As String = ";"      'Constant variable ~                        ending the line
        Dim strVar As String            'String containing stringified numeric data (for processing the entries in the DATA file)
        Dim bytStart As Byte            'Start position in the string for extracting a subentry
        Dim bytEnd As Byte              'End position ~
        Dim bytLen As Byte              'The length of the string to extract
        Const bytDataEnt As Byte = 18   'Amount of entries per line

        Dim bytErrFlag As Byte = 0      'Counts the number of invalid entries, if = bytDataEnt bad entries, disable THPDec, ThpRip, and ThpEnc group boxes

        Try
            bytItems = 0
            xFileData = File.OpenText(strPATH & strBAK & LISTING)   'Open the LISTING file

            'Count the amount of items
            While xFileData.EndOfStream() <> True
                strEntry = xFileData.ReadLine()                   'Read a line from the file
                bytItems += 1
            End While
            ReDim THPs(bytItems)                                  'Redim the THPs array appropriately
            'Close the LISTING file
            xFileData.Close()
            xFileData.Dispose()
            xFileData = Nothing

            'Load all relative file paths into THPs(#).File
            xFileData = File.OpenText(strPATH & strBAK & LISTING)   'Open the LISTING file
            For bytCtr1 = 1 To bytItems Step 1                      'Iterate through all of the lines
                strEntry = xFileData.ReadLine()                     'Read a line
                THPs(bytCtr1).File = strEntry                       'Dump file paths into appropriate array entry
                cmbTHP.Items.Add(THPs(bytCtr1).File)                'Dump data into Combo box
            Next bytCtr1
            'Close the LISTING file
            xFileData.Close()
            xFileData.Dispose()
            xFileData = Nothing

            'Load all descriptions into THPs(#).Desc array
            xFileData = File.OpenText(strPATH & strBAK & DESC)      'Open the DESC file
            For bytCtr1 = 1 To bytItems Step 1                      'Iterate through all lines
                strEntry = xFileData.ReadLine()                     'Read a line
                THPs(bytCtr1).Desc = strEntry                       'Dump the data into the array slots
            Next bytCtr1
            'Close the DESC file
            xFileData.Close()
            xFileData.Dispose()
            xFileData = Nothing

            'Load all ctrl descriptions into THPs(#).visual.cdesc array
            xFileData = File.OpenText(strPATH & strBAK & CDESC)     'Open the CDESC file
            For bytCtr1 = 1 To bytItems Step 1                      'Iterate through all lines
                strEntry = xFileData.ReadLine()                     'Read a line
                THPs(bytCtr1).visual.CDesc = strEntry               'Dump the data into the array slots
            Next bytCtr1
            'Close the DESC file
            xFileData.Close()
            xFileData.Dispose()
            xFileData = Nothing
            '-------------------------
            'Get data from the DATA file

            'Load all AV data into THPs(#).visual and THPs(#).audial struct elements
            xFileData = File.OpenText(strPATH & strBAK & DATA)      'Load the DATA file
            For bytCtr1 = 1 To bytItems Step 1                      'Iterate through all lines
                strEntry = xFileData.ReadLine()                     'Read a line

                bytStart = 1                                        'Init start pos
                'Parse each of the entries in each line
                bytErrFlag = 0
                For bytCtr2 = 1 To bytDataEnt Step 1                'Iterate through all entries in line

                    'If not the last data entry in line, find the position of the SEP character (,), else SEP2 character (;)
                    If bytCtr2 <> bytDataEnt Then bytEnd = InStr(bytStart, strEntry, SEP) Else bytEnd = InStr(bytStart, strEntry, SEP2) 'Record the position of the next SEP1 character,
                    bytLen = (bytEnd - bytStart)             'Get the length of the sub entry (subtract End from Start)
                    strVar = Mid(strEntry, bytStart, bytLen) 'Extract the sub entry via MID command

                    'If the entry is 0, increment error counter
                    If bytCtr2 = 14 Then
                        If TryParseErr_Single(strVar) = 0 Then bytErrFlag += 1
                    Else
                        If TryParseErr_UShort(strVar) = 0 Then bytErrFlag += 1
                    End If

                    'Allocate the extracted value into the appropriate array data fields based on index
                    Select Case bytCtr2

                        'Total THP video width/height
                        Case 1
                            THPs(bytCtr1).visual.TDim.width = TryParseErr_UShort(strVar)
                        Case 2
                            THPs(bytCtr1).visual.TDim.height = TryParseErr_UShort(strVar)

                            'THP subvideo array info
                            'Row, Col, R*C, Multiplicity, mult optional?, r*c*m
                        Case 3
                            THPs(bytCtr1).visual.THPinfo.row = TryParseErr_Byte(strVar)
                        Case 4
                            THPs(bytCtr1).visual.THPinfo.col = TryParseErr_Byte(strVar)
                        Case 5
                            THPs(bytCtr1).visual.THPinfo.subV = TryParseErr_Byte(strVar)
                        Case 6
                            THPs(bytCtr1).visual.THPinfo.mult = TryParseErr_Byte(strVar)
                        Case 7
                            THPs(bytCtr1).visual.THPinfo.subVT = TryParseErr_Byte(strVar)

                            'Subvideo info
                            'Subvideo width and height
                        Case 8
                            THPs(bytCtr1).visual.SDim.width = TryParseErr_UShort(strVar)
                        Case 9
                            THPs(bytCtr1).visual.SDim.height = TryParseErr_UShort(strVar)

                            'Frame counts for each subvideo, total THP video
                        Case 10
                            THPs(bytCtr1).visual.Frames.subframes = TryParseErr_UShort(strVar)
                        Case 11
                            THPs(bytCtr1).visual.Frames.totframes = TryParseErr_UShort(strVar)

                            'Width and height of padding
                        Case 12
                            THPs(bytCtr1).visual.Padding.width = TryParseErr_UShort(strVar)
                        Case 13
                            THPs(bytCtr1).visual.Padding.height = TryParseErr_UShort(strVar)
                        Case 14
                            'FPS as single                            
                            THPs(bytCtr1).visual.FPS = TryParseErr_Single(strVar)

                            'Control/Audio info
                            'Has control signal?, has audio?, is stereo?, audio freq
                        Case 15
                            THPs(bytCtr1).visual.Ctrl = BitToBool(TryParseErr_Byte(strVar))
                        Case 16
                            THPs(bytCtr1).audial.has = BitToBool(TryParseErr_Byte(strVar))
                        Case 17
                            THPs(bytCtr1).audial.Stereo = BitToBool(TryParseErr_Byte(strVar))
                        Case 18
                            THPs(bytCtr1).audial.freq = TryParseErr_UShort(strVar)
                    End Select

                    bytStart = bytEnd + 1 'Increment the start position to 1 past the located SEP1 character
                Next bytCtr2 'Repeat for the other entries in the line

                'Set bad flag as appropriately
                If bytErrFlag = bytDataEnt Then
                    THPs(bytCtr1).Bad = True
                Else
                    THPs(bytCtr1).Bad = False
                End If

            Next bytCtr1 'Repeat for all lines

            'Close the DATA file
            xFileData.Close()
            xFileData.Dispose()
            xFileData = Nothing
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Data file parsing/IO error!")
        End Try
    End Sub

    ''' <summary>
    ''' Handles dumping the data from the data files (now in RAM) into the appropriate fields for the THPInfo Group box, when the combo box has been changed
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub cmbTHP_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles cmbTHP.SelectedIndexChanged
        Try
            'Index in the THP combo box
            Dim bytEntry As Byte = cmbTHP.SelectedIndex + 1

            'Set THPEnc and THPDec group boxes to visible as appropriately, depending on bad state
            Dim state As Boolean = THPs(bytEntry).Bad
            state = Not (state)
            grpTHPEnc.Visible = state
            grpTHPDec.Visible = state

            'Update stats for current THP
            'Total video width and height
            txtTDims_W.Text = THPs(bytEntry).visual.TDim.width.ToString()
            txtTDims_H.Text = THPs(bytEntry).visual.TDim.height.ToString()

            'THP Array info: row, column, amount of subvids (=r*c)
            txtArr_R.Text = THPs(bytEntry).visual.THPinfo.row.ToString()
            txtArr_C.Text = THPs(bytEntry).visual.THPinfo.col.ToString()
            txtArr_S.Text = THPs(bytEntry).visual.THPinfo.subV.ToString()

            'Video multiplicity info: Amount of mult, optional? 
            txtVM_M.Text = THPs(bytEntry).visual.THPinfo.mult.ToString()

            'Total amount of subvideos (r*c*multiplicity)
            txtV_TSubs.Text = THPs(bytEntry).visual.THPinfo.subVT.ToString()

            'Sizes of the subvideos and padding (width x height in px)
            txtVS_W.Text = THPs(bytEntry).visual.SDim.width.ToString()
            txtVS_H.Text = THPs(bytEntry).visual.SDim.height.ToString()
            txtVP_W.Text = THPs(bytEntry).visual.Padding.width.ToString()
            txtVP_H.Text = THPs(bytEntry).visual.Padding.height.ToString()

            'Number of frames in the video: frames in each subvideo (each m), and total for the THP file (t=s*m)
            txtVF_S.Text = THPs(bytEntry).visual.Frames.subframes.ToString()
            txtVF_T.Text = THPs(bytEntry).visual.Frames.totframes.ToString()

            'Other playback info. FPS, does video have control signal?, Text desc of the control signal usage
            txtVC_F.Text = THPs(bytEntry).visual.FPS.ToString("f")
            txtVC_C.Text = THPs(bytEntry).visual.Ctrl.ToString()
            txtVC_D.Text = THPs(bytEntry).visual.CDesc

            'Audio info. Has audio?, Stereo?, frequency of audio (usually 32kHz)
            txtA_A.Text = THPs(bytEntry).audial.has.ToString
            txtA_S.Text = THPs(bytEntry).audial.Stereo.ToString
            txtA_F.Text = THPs(bytEntry).audial.freq.ToString

            'Text desc about the THP video file
            txtFDesc.Text = THPs(bytEntry).Desc

            'Prepare THPEnc/Dec fields
            HandleArrState()                                'Show naming conventions for this THP file
            HandleRipTimeMasks()                            'Updates the masks for the start/end lengths for ripping (time)
            txtTE_D.Text = txtVF_T.Text.Length.ToString()   'Set default value in THPEnc for digits, based on the string.length of the video's total frames
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in cmbTHP_SelectedIndexChanged!")
        End Try
    End Sub

    ''' <summary>
    ''' Does this THP have padding?
    ''' </summary>
    ''' <returns>Padding?</returns>
    ''' <remarks></remarks>
    Private Function THPHasPad() As Boolean
        Dim outp As Boolean = False                 'Output
        Try
            Dim d As Dims                               'Dims
            d.width = TryParseErr_UShort(txtVP_W.Text)  'Width=Video padding width
            d.height = TryParseErr_UShort(txtVP_H.Text) 'Height=Video padding height

            'If both dims are not zero, then hasPadding
            If d.width <> 0 And d.height <> 0 Then outp = True
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in THPHasPad()")
        End Try
        Return outp
    End Function

    ''' <summary>
    ''' Does this THP have audio?
    ''' </summary>
    ''' <returns>Audio?</returns>
    ''' <remarks></remarks>
    Private Function THPHasAudio() As Boolean
        Dim has As String = txtA_A.Text                 'Get hasAudio field as string
        Dim hasAudio As Boolean = BoolStrToBool(has)    'Does vid have audio? (Convert has to bool)
        Return hasAudio
    End Function

    '===========================
    'Options Tab stuff

    ''' <summary>
    ''' Hnadles loading the THP root dir
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnLoadRoot_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnBrowseRoot.Click
        Try
            'Load the LoadTHPRoot Load Dialog Box, user selects root directory of THP
            If LoadTHPRoot.ShowDialog() = Windows.Forms.DialogResult.Cancel Then Exit Sub
            txtRoot.Text = LoadTHPRoot.SelectedPath    'Dump the path into the textbox, for later retrieval
            CheckPathsSet()                             'Handle enabling THP Tab
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in btnLoadRoot_Click!")
        End Try
    End Sub

    ''' <summary>
    ''' Handles loading Irfanview
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btniView_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btniView.Click
        'Load the LoadiView ofd, user selects i_view32.exe
        Try
            If LoadiView.ShowDialog() = Windows.Forms.DialogResult.Cancel Then Exit Sub
            txtiView.Text = LoadiView.FileName      'Dump the path into the textbox, for later retrieval
            CheckPathsSet()                             'Handle enabling THP Tab
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in btniView_Click!")
        End Try
    End Sub

    ''' <summary>
    ''' Handles loading the FFMPeg exe path
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnBrowseFFMpeg_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnBrowseFFMpeg.Click
        Try
            'Load the LoadFMPegRoot Load Dialog Box, user selects root directory of FFMpeg exes
            If LoadFMPegRoot.ShowDialog() = Windows.Forms.DialogResult.Cancel Then Exit Sub
            txtFFMpeg.Text = LoadFMPegRoot.SelectedPath    'Dump the path into the textbox, for later retrieval
            CheckPathsSet()                             'Handle enabling THP Tab
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in btnBrowseFFMpeg_Click!")
        End Try
    End Sub

    ''' <summary>
    ''' Handles loading the THPConv exe file
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnBrowseTHPConv_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnBrowseTHPConv.Click
        'Load the LoadTHPConv ofd, user selects thpconv.exe
        Try
            If LoadTHPConv.ShowDialog() = Windows.Forms.DialogResult.Cancel Then Exit Sub
            txtTHPConv.Text = LoadTHPConv.FileName      'Dump the path into the textbox, for later retrieval
            CheckPathsSet()                             'Handle enabling THP Tab
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in btnBrowseTHPConv_Click!")
        End Try
    End Sub

    ''' <summary>
    ''' If the options have been filled in, enable elements in THP tab
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub CheckPathsSet()
        'Options det to be filled if something
        If (txtRoot.Text <> Nothing) And (txtFFMpeg.Text <> Nothing) And (txtiView.Text <> Nothing) And (txtTHPConv.Text <> Nothing) Then
            'Make everything in the THP tab visible now (THPFile lable and combo box, whole THP Info group box)
            lblTHPFile.Visible = True
            cmbTHP.Visible = True
            grpTHPInfo.Visible = True
        End If
    End Sub

    ''' <summary>
    ''' 'Handles clicking of About button, showing the box
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnAbout_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnAbout.Click
        Try
            My.Computer.Audio.Play(My.Resources.EagleSoft, AudioPlayMode.Background)    'Play "EagleSoft Ltd"
            About.ShowDialog()                                                          'Show the about box
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error with showing About box!")
        End Try
    End Sub

    '===========================
    'THP Viewer/Ripper group box

    ''' <summary>
    ''' Handle THP playback
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnPlay_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnPlay.Click
        Try
            Dim startInfo As ProcessStartInfo
            startInfo = New ProcessStartInfo

            'Bug info: https://forum.videohelp.com/threads/388189-ffplay-WASAPI-can-t-initialize-audio-client-error
            'If DirectSound option checked, set SDL_AUDIODRIVE = directsound. This is a workaround to a ffmpeg bug to allow audio
            If chkRip_DSound.Checked = True Then
                'https://social.msdn.microsoft.com/Forums/vstudio/en-US/a18210d7-44f4-4895-8bcc-d3d1d26719e5/setting-environment-variable-from-vbnet?forum=netfxbcl
                'set SDL_AUDIODRIVER=directsound
                startInfo.EnvironmentVariables("SDL_AUDIODRIVER") = "directsound"
            End If

            Dim cmd As String = strQUOT & txtFFMpeg.Text & strBAK & exeFPlay & strQUOT  'FFPlay command "C:\FDIR\ffplay.exe"
            Dim args As String = strQUOT & txtRoot.Text & cmbTHP.Text & strQUOT         'Arguments for FFPLlay "C:\Path\to\THPRoot\file.THP"

            'Run the cmd
            startInfo.FileName = cmd & args
            startInfo.UseShellExecute = False
            Dim shell As Process
            shell = New Process
            shell.StartInfo = startInfo
            shell.Start()
            shell.WaitForExit()
            shell.Close()
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error during playback!")
        End Try
    End Sub

    ''' <summary>
    ''' Handles ripping a THP to MP4(+WAV) and dummy frames for padding
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnRip_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnRip.Click
        'Open ofdRip, user selects path/base filename
        Try
            Dim inFile As String = txtRoot.Text & cmbTHP.Text   'Input file. C:\PathToTHP\DIRtoTHP\file.thp"
            Dim initDir As String = FileDir(inFile)             'Initial directory. Directory of inFile
            Dim newFile As String = FileAndExt(cmbTHP.Text)     'New file. "Filename.thp" from inFile
            Dim file As String = ""
            Dim file2 As String = ""
            Dim type As Boolean = chkRipDumF.Checked            'Type of ripping to do. True=Rip dummy frames
            newFile = newFile.Replace(".thp", "")               'Remove extension from newFile, just get filename-ext

            Dim suffix As String = GetCellFrameName()           'Suffix for cell name (if any)
            ofdRip.FileName = newFile & suffix                  'Set ofd box filename to newFile & suffix
            ofdRip.InitialDirectory = initDir                   'Set ofd init dir to initDir

            'Show the DBox
            If ofdRip.ShowDialog() = Windows.Forms.DialogResult.Cancel Then Exit Sub
            Dim outFile As String = ofdRip.FileName                 'Output file. C:\PathToFile\file.mp4
            Dim tempFile As String = FileDir(outFile) & "temp.mp4"  'Temp file
            Dim outPath As String = FileDir(outFile)                'Output path. Path of outFile
            Dim outFilename As String = FileAndExt(outFile)         'Output filename. Filename.mp4

            'Video Conv: ffmpeg -i input_video.mp4 output.mp4
            'https://video.stackexchange.com/questions/4563/how-can-i-crop-a-video-with-ffmpeg
            'Video Crop: ffmpeg -i in.mp4 -filter:v "crop=out_w:out_h:x:y" out.mp4
            'https://www.bugcodemaster.com/article/extract-audio-video-using-ffmpeg
            'Audio Extraction: ffmpeg -i input_video.mp4 -vn output_audio.mp3

            'https://superuser.com/questions/459313/how-to-cut-at-exact-frames-using-ffmpeg
            'Cut video from start to end frame: -vf select="between(n\,200\,300),setpts=PTS-STARTPTS"

            Dim cmd As String = ""                              'Command to run
            Dim x As String = txtTD_CX.Text                     'Crop xpos
            Dim y As String = txtTD_CY.Text                     'Crop ypos
            Dim w As String = txtTD_CW.Text                     'Crop width
            Dim h As String = txtTD_CH.Text                     'Crop height
            Dim _start As UShort = txtTD_FS.Text                'frame start
            Dim _end As UShort = txtTD_FE.Text                  'frame end

            'User-error:
            'If chkRipDumF is checked (therefore, multiplicity=0 and Dum radio button),
            'and start/end frames do not match THP vids' min/max, throw error.
            'This will force users to set those values as such, to ensure ripping of dummy frames work
            'I'm lazy :P
            If type = True Then
                Dim min As UShort = 1
                Dim max As UShort = TryParseErr_UShort(txtVF_T.Text)
                If _start <> min Or _end <> max Then
                    Throw New System.Exception("When ripping the dummy frames with a multiplicity of 0, please ensure the start/end timeframe values equal the THP video's min/max frame values! This will allow proper ripping of each unique dummy frame for each multiplicity.")
                End If
            End If

            'Step 1: Convert THP to temp MP4. Encoded THP to H264 MP4 with crop filter
            '"C:\FFMPegPath\ffmpeg.exe"
            cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT
            ' -i C:\PathToTHP\DIRtoTHP\file.thp -vcodec h264 -y -filter:v "crop=out_w:out_h:x:y" "C:\OutputDir\output.mp4"
            cmd &= " -y -i " & strQUOT & inFile & strQUOT & " -vcodec h264 -filter:v " & strQUOT & "crop=" & w & ":" & h & ":" & x & ":" & y & strQUOT & " " & strQUOT & tempFile & strQUOT

            'Run the cmd
            Dim startInfo As ProcessStartInfo
            startInfo = New ProcessStartInfo
            startInfo.FileName = cmd
            startInfo.UseShellExecute = False
            Dim shell As Process
            shell = New Process
            shell.StartInfo = startInfo
            shell.Start()
            shell.WaitForExit()

            'Step 2: Convert temp mp4 to final MP4, cutting between start and end frames
            '"C:\FFMPegPath\ffmpeg.exe"
            cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT
            ' -y -i C:\PathToTHP\DIRtoTHP\file.thp -vcodec h264 -an -vf select="between(n\,start_frame\,end_frame),setpts=PTS-STARTPTS" "C:\OutputDir\output.mp4"
            cmd &= " -y -i " & strQUOT & tempFile & strQUOT & " -vcodec h264 -an -vf select=" & strQUOT & "between(n" & strBAK & "," & _start & strBAK & "," & _end & "),setpts=PTS-STARTPTS" & strQUOT
            cmd &= " " & strQUOT & outFile & strQUOT

            'Run the cmd
            startInfo = New ProcessStartInfo
            startInfo.FileName = cmd
            startInfo.UseShellExecute = False
            shell = New Process
            shell.StartInfo = startInfo
            shell.Start()
            shell.WaitForExit()

            'Extract audio as wav (if any)
            Dim hasAudio As Boolean = THPHasAudio()
            If hasAudio Then
                'If THP has audio

                'If DirectSound checked, do SDL driver workaround
                If chkRip_DSound.Checked = True Then
                    'set SDL_AUDIODRIVER=directsound
                    startInfo.EnvironmentVariables("SDL_AUDIODRIVER") = "directsound"
                End If

                'Convert the audio to file.wav file
                '"C:\FFMPegPath\FFMPEG.exe"
                cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT
                ' -i "C:\PathToTHP\DIRtoTHP\file.thp" -vn "C:\OutputDir\file.wav"
                cmd &= " -y -i " & strQUOT & inFile & strQUOT & " -vn "
                cmd &= strQUOT & outPath & FileAndExt(inFile).Replace(".thp", ".wav") & strQUOT
                'Run the cmd
                startInfo.FileName = cmd
                shell.StartInfo = startInfo
                shell.Start()
                shell.WaitForExit()


            End If

            If type = True Then
                'If ripping dummy ctrl frames.
                'Convert the cropped MP4 file (cropped to the ctrl area) to bmp frames ("dummyTemp_%0Nd.bmp"),
                'Keep only 1st frame for each multiplicty, rename to "dummy_N.bmp", delete excess frames

                '"C:\FFMPegPath\FFMPEG.exe" -y 
                cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y "

                'Output ctrl MP4 to .bmp frames
                Dim d As String = ""                                                    'Printf digit formatter thingy (pad to N digits)
                Dim dgs As UShort = 0                                                   'Amount of digits for printf formatter thingy            
                dgs = TryParseErr_UShort(txtVF_T.Text.Length)                           'Set digits to the amount of digits for the total amount of frames in the video
                d = "%0" & dgs.ToString() & "d"                                         'Set the printf digit formatter to "dgs" digits
                cmd &= "-i " & strQUOT & outFile & strQUOT                              '-i "C:\OutputDir\file.mp4"

                '"C:\OutputDir\dummyTemp_%0Nd.bmp"
                file = strQUOT & FileDir(outFile) & "dummyTemp_" & d & ".bmp" & strQUOT
                cmd &= " " & file

                'Run cmd
                startInfo.FileName = cmd
                shell.StartInfo = startInfo
                shell.Start()
                shell.WaitForExit()

                'Rename the appropriate frames to "dummy_N.bmp", remove the others 
                Dim i As Byte = 0                           'Generic iterator
                Dim j As UShort = 0                         'Frame value
                Dim frames As UShort = TryParseErr_UShort(txtVF_S.Text)    'The amount of frames per subvideo
                Dim m As Byte = TryParseErr_Byte(txtVM_M.Text)             '0-based multiplicity value
                m -= 1

                'Iterate through the mults (0-based)
                For i = 0 To m Step 1
                    j = i * frames                                      'Frame ID = multiplicity ID * amount of frames. This gets 1st frame for each multplicity.
                    j += 1                                              'Make FrameID 1-based
                    d = "_" & j.ToString(StrDup(dgs, "0")) & ".bmp"     'Set d as the frame ID string "_%0Nd.bmp"
                    file = "dummy_" & (i + 1).ToString() & ".bmp"       'File = "dummy_N.bmp"

                    'Move file "C:\OutputDir\dummyTemp_ID.bmp" to "C:\OutputDir\dummy_N.bmp"
                    file = FileDir(outFile) & FileAndExt(file)          'File = "C:\OutputDir\dummy_N.bmp"
                    file2 = FileDir(outFile) & "dummyTemp" & d          'File2 = "C:\OutputDir\dummyTemp_ID.bmp"
                    My.Computer.FileSystem.MoveFile(file2, file, True)
                Next i

                'Delete all extra "dummyTemp_%0Nd.bmp" files
                file = FileDir(outFile)                 'file = C:\WorkingDir
                file2 = "dummyTemp*.bmp"                'file2 = dummyTemp*.bmp
                DeleteFilesFromFolder(file, file2)
            End If

            'Delete temp.mp4
            DeleteFilesFromFolder(FileDir(outFile), "temp.mp4")

            'Thwimp kicks dat Koopa shell away!
            shell.Close()
            MsgBox("Video ripped!", MsgBoxStyle.Information, "Success!")
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error during ripping!")
        End Try
    End Sub

    ''' <summary>
    ''' Keeps txtTD_CX in range for total vid width
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub txtTD_CX_Validated(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTD_CX.Validated
        Try
            Dim xmin As UShort = 0                                      'xmin = 0
            Dim xmax As UShort = TryParseErr_UShort(txtTDims_W.Text)    'xmax = Total vid width
            xmax -= 1                                                   'Make it 0-based. (Can't do a crop at xpos=xmax)
            txtTD_CX.Text = KeepInRange(txtTD_CX.Text, xmin, xmax)      'Set string within numeric range
            KeepWInRange()                                              'Keep W in range
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in txtTD_CX_Validated!")
        End Try
    End Sub
    ''' <summary>
    ''' Keeps txtTD_CY in range for total vid height
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub txtTD_CY_Validated(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTD_CY.Validated
        Try
            Dim ymin As UShort = 0                                      'ymin = 0
            Dim ymax As UShort = TryParseErr_UShort(txtTDims_H.Text)    'ymax = Total vid height
            ymax -= 1                                                   'Make it 0-based. (Can't do a crop at ypos=ymax)
            txtTD_CY.Text = KeepInRange(txtTD_CY.Text, ymin, ymax)      'Set string within numeric range
            KeepHInRange()                                              'Keep H in range
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in txtTD_CY_Validated!")
        End Try
    End Sub
    ''' <summary>
    ''' Keeps txtTD_CW in range for x offset and total vid width
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub txtTD_CW_Validated(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTD_CW.Validated
        KeepWInRange()
    End Sub
    ''' <summary>
    ''' Keeps txtTD_CH in range for y offset and total vid height
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub txtTD_CH_Validated(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTD_CH.Validated
        KeepHInRange()
    End Sub

    ''' <summary>
    ''' Keeps W of crop value in range
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub KeepWInRange()
        Try
            Dim wmin As UShort = 0                                  'wmin = 0
            Dim w1 As UShort = TryParseErr_UShort(txtTD_CX.Text)    'a = xpos
            Dim w2 As UShort = TryParseErr_UShort(txtTDims_W.Text)  'b = total video width
            Dim wmax As UShort = w2 - w1                            'Get dif of b-a as wmax
            txtTD_CW.Text = KeepInRange(txtTD_CW.Text, wmin, wmax)  'Keep w in range
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in KeepWInRange()")
        End Try
    End Sub

    ''' <summary>
    ''' Keeps H of crop value in range
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub KeepHInRange()
        Try
            Dim hmin As UShort = 0                                  'wmin = 0
            Dim h1 As UShort = TryParseErr_UShort(txtTD_CY.Text)    'a = ypos
            Dim h2 As UShort = TryParseErr_UShort(txtTDims_H.Text)  'b = total video height
            Dim hmax As UShort = h2 - h1                            'Get dif of b-a as hmax
            txtTD_CH.Text = KeepInRange(txtTD_CH.Text, hmin, hmax)  'Keep h in range
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in KeepHInRange()")
        End Try
    End Sub

    ''' <summary>
    ''' Updates the start/end time frame masks, based on the THP total frame length
    ''' </summary>
    Private Sub HandleRipTimeMasks()
        Try
            Dim length As Byte = txtVF_T.Text.Length    'Get length as byte for total frames in video
            Dim mask As String = StrDup(length, "0")    'The mask, set to length of 0s
            'Update the start/end masks
            txtTD_FS.Mask = mask
            txtTD_FE.Mask = mask

            'Force the mult NUD to 0, to fire an event to update the default start/end frame values for ripping whole video
            'This doesn't seem to always want to fire; forcibly call the function
            nudTD_M.Value = 0
            nudTD_M_ChangeMe()
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in HandleRipTimeMasks()!")
        End Try
    End Sub

    Private Sub nudTD_M_ValueChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles nudTD_M.ValueChanged
        nudTD_M_ChangeMe()
    End Sub
    ''' <summary>
    ''' Updates the default start/end frame rip values when the multiplicity NUD is changed, the chkRipM value, and the chkRipDumF value
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub nudTD_M_ChangeMe()
        Try
            Dim m As Byte = TryParseErr_Byte(nudTD_M.Value)         'Current multiplicity index
            Dim mult As UShort = TryParseErr_UShort(txtVF_S.Text)   'Amount of frames in a subvideo
            Dim _start As UShort = 1                                'Start frame
            Dim _end As UShort = 2                                  'End frame
            Dim ma As Byte = 0                                      'Start multiplicity index
            Dim mb As Byte = 1                                      'End multiplicity index

            'Flag to indicate bugfix. THPs with only one frame can use either m=0 (all frames, file named [file].mp4),
            'or m=1 (1st and only frame, file named "file_A1_1.mp4").
            'Flag inidicates to fix an off-by-one error if m=1 for this special case for the end time frame            
            Dim SingleBugfix As Boolean = False
            'If nud has a min of 0 and max of 1, then special bugfix
            If nudTD_M.Minimum = 0 And nudTD_M.Maximum = 1 Then SingleBugfix = True

            'Zero is special case meaning to rip all frames (frame 1 to total)
            Dim singleM As Boolean = True                           'Rip only one multiplicity?
            Dim dumF As Boolean = False                             'Rip dummy frames?
            If m = 0 Then singleM = False 'If m=0, ripping multiple Ms
            If singleM = False Then
                'Set range from 1 to final frame
                _start = 1
                _end = TryParseErr_Single(txtVF_T.Text)
                If radTD_Dum.Checked = True Then dumF = True 'If dummy rad is checked, then set dumF flag
            Else
                mb = nudTD_M.Value                                  'mult index in box
                ma = mb - 1                                         'index-- (0-based mult index)
                _start = ma * mult                                  'start = (start index * frame mult)
                If _start = 0 Then _start = 1 '1st frame is one-based; set to 1 if 0
                _end = mb * mult                                    'end = (end index * frame mult) - 1

                'Only decrement by one if not the special bugfix
                If SingleBugfix = False Then _end = _end - 1
            End If

            'Update the start/end frame text values, chkRipM state/text, chkRipDumF
            chkRipM_Change(singleM)
            chkRipDumF.Checked = dumF
            txtTD_FS.Text = _start.ToString()
            txtTD_FE.Text = _end.ToString()
        Catch ex As Exception
            'MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in nudTD_M_ValueChanged()!")
        End Try
    End Sub

    ''' <summary>
    ''' Changes checked value and text of chkRipM
    ''' </summary>
    ''' <param name="val">New state</param>
    ''' <remarks></remarks>
    Private Sub chkRipM_Change(ByVal val As Boolean)
        chkRipM.Checked = val
        ChkString("Single", "All", chkRipM)
    End Sub

    ''' <summary>
    ''' Keeps the frame start rip value within range (start={start|(1≤start≤total ∩ start&lt;end})
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub txtTD_FS_ValidatedByVal(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTD_FS.Validated
        Try
            '1st statement
            Dim smin As UShort = 1                                      'smin = 1
            Dim smax As UShort = TryParseErr_UShort(txtVF_T.Text)       'smax = Total amt of frames in vid
            Dim _end As UShort = TryParseErr_UShort(txtTD_FE.Text)      'end frame length
            txtTD_FS.Text = KeepInRange(txtTD_FS.Text, smin, smax)      'Set string within numeric range

            '2nd statement
            smin = 1
            smax = _end - 1
            txtTD_FS.Text = KeepInRange(txtTD_FS.Text, smin, smax)      'Set string within numeric range
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in txtTD_FS_Validated!")
        End Try
    End Sub

    ''' <summary>
    ''' Keeps the frame end rip value within range (end={end|(1≤end≤total ∩ end&gt;start})
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub txtTD_FE_Validated(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTD_FE.Validated
        Try
            '1st statement
            Dim emin As UShort = 1                                      'emin = 1
            Dim emax As UShort = TryParseErr_UShort(txtVF_T.Text)       'emax = Total amt of frames in vid
            Dim _start As UShort = TryParseErr_UShort(txtTD_FS.Text)    'start frame length
            txtTD_FE.Text = KeepInRange(txtTD_FE.Text, emin, emax)      'Set string within numeric range

            '2nd statement
            emin = _start + 1
            txtTD_FE.Text = KeepInRange(txtTD_FE.Text, emin, emax)      'Set string within numeric range
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in txtTD_FE_Validated!")
        End Try
    End Sub

    ''' <summary>
    ''' Update the pos/size of the crop box params based on the video cell selected for THP Decoding
    ''' </summary>
    Private Sub radTD_A1_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_A1.CheckedChanged
        If radTD_A1.Checked = True Then HandleTimeFrameCell(1, 1, 0)
    End Sub
    Private Sub radTD_A2_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_A2.CheckedChanged
        If radTD_A2.Checked = True Then HandleTimeFrameCell(2, 1, 0)
    End Sub
    Private Sub radTD_A3_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_A3.CheckedChanged
        If radTD_A3.Checked = True Then HandleTimeFrameCell(3, 1, 0)
    End Sub
    Private Sub radTD_A4_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_A4.CheckedChanged
        If radTD_A4.Checked = True Then HandleTimeFrameCell(4, 1, 0)
    End Sub
    Private Sub radTD_A5_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_A5.CheckedChanged
        If radTD_A5.Checked = True Then HandleTimeFrameCell(5, 1, 0)
    End Sub
    Private Sub radTD_A6_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_A6.CheckedChanged
        If radTD_A6.Checked = True Then HandleTimeFrameCell(6, 1, 0)
    End Sub
    Private Sub radTD_B1_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_B1.CheckedChanged
        If radTD_B1.Checked = True Then HandleTimeFrameCell(1, 2, 0)
    End Sub
    Private Sub radTD_B2_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_B2.CheckedChanged
        If radTD_B2.Checked = True Then HandleTimeFrameCell(2, 2, 0)
    End Sub
    Private Sub radTD_B3_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_B3.CheckedChanged
        If radTD_B3.Checked = True Then HandleTimeFrameCell(3, 2, 0)
    End Sub
    Private Sub radTD_B4_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_B4.CheckedChanged
        If radTD_B4.Checked = True Then HandleTimeFrameCell(4, 2, 0)
    End Sub
    Private Sub radTD_B5_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_B5.CheckedChanged
        If radTD_B5.Checked = True Then HandleTimeFrameCell(5, 2, 0)
    End Sub
    Private Sub radTD_B6_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_B6.CheckedChanged
        If radTD_B6.Checked = True Then HandleTimeFrameCell(6, 2, 0)
    End Sub    
    Private Sub radTD_All_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_All.CheckedChanged
        If radTD_All.Checked = True Then HandleTimeFrameCell(0, 0, 1)
    End Sub

    Private Sub radTD_Dum_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles radTD_Dum.CheckedChanged
        If radTD_Dum.Checked = True Then HandleTimeFrameCell(0, 0, -1)
        nudTD_M_ChangeMe()  'Additionally force-run this, to set the chkRipDumF box if necessary
    End Sub

    ''' <summary>
    ''' Function which handles updating the start/end frame counts based on the radio button array for individual THP cells
    ''' </summary>
    ''' <param name="row">1-based ID of row</param>
    ''' <param name="col">1-based ID of col</param>
    ''' <param name="special">Special radio button. -1=dum, 0=use r/c pair, 1=All</param>
    ''' <remarks></remarks>
    Private Sub HandleTimeFrameCell(ByVal row As Byte, ByVal col As Byte, ByVal special As SByte)
        Try
            'Total size of the THP video
            Dim VidTSize As Dims
            VidTSize.width = TryParseErr_UShort(txtTDims_W.Text)    'Tot Width
            VidTSize.height = TryParseErr_UShort(txtTDims_H.Text)   'Tot Height

            'Frame size of the THP subvideo cells
            Dim VidFSize As Dims
            VidFSize.width = TryParseErr_UShort(txtVS_W.Text)    'Frame Width
            VidFSize.height = TryParseErr_UShort(txtVS_H.Text)   'Frame Height

            'Padding size of the THP video cells
            Dim PadSize As Dims
            PadSize.width = TryParseErr_UShort(txtVP_W.Text)     'Pad Width
            PadSize.height = TryParseErr_UShort(txtVP_H.Text)    'Pad Height

            'Crop box params to change
            Dim pos As Dims                                     'X/Y Position for cropping. This is zero-based!
            Dim size As Dims                                    'Size for cropping

            If special = 0 Then
                'Handle row/col pair

                'Set appropriate pos
                pos.width = (col - 1) * VidFSize.width          'Width = 0-based_col * frame_width
                pos.height = (row - 1) * VidFSize.height        'Width = 0-based_row * frame_height
                'Set appropriate size. Always frame size for this option
                size.width = VidFSize.width
                size.height = VidFSize.height
            ElseIf special = -1 Then
                'Handle dummy

                pos.width = 0                                   'Start at x=0
                pos.height = VidTSize.height - PadSize.height   'Start at y=Total video height - padding height
                'Set size to padsize
                size.width = PadSize.width
                size.height = PadSize.height
            Else
                'Handle All. At origin, full vid size

                pos.width = 0
                pos.height = 0
                'Set size to total video size
                size.width = VidTSize.width
                size.height = VidTSize.height
            End If

            'Update the text boxes as appropriate with the new, adjusted params
            txtTD_CX.Text = pos.width.ToString()
            txtTD_CY.Text = pos.height.ToString()
            txtTD_CW.Text = size.width.ToString()
            txtTD_CH.Text = size.height.ToString()
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in HandleTimeFrameCell()!")
        End Try
    End Sub

    ''' <summary>
    ''' Gets the suffix name and mult for the current selected frame cell in the decoder/ripper group box.
    ''' Used for setting the default filename when ripping
    ''' </summary>
    ''' <returns>Suffix for checked video cell/special</returns>
    Private Function GetCellFrameName() As String
        Dim nameResult As String = ""                       'String result of cell name (cell+suffix)
        Dim suffix As String = ""                           'Multiplicity suffix
        Try
            'Array of radio buttons (A1N notation)
            Dim Rads(6, 2) As System.Windows.Forms.RadioButton
            Rads(1, 1) = radTD_A1
            Rads(2, 1) = radTD_A2
            Rads(3, 1) = radTD_A3
            Rads(4, 1) = radTD_A4
            Rads(5, 1) = radTD_A5
            Rads(6, 1) = radTD_A6
            Rads(1, 2) = radTD_B1
            Rads(2, 2) = radTD_B2
            Rads(3, 2) = radTD_B3
            Rads(4, 2) = radTD_B4
            Rads(5, 2) = radTD_B5
            Rads(6, 2) = radTD_B6
            Dim Rad_Dum = radTD_Dum 'Dummy radio button
            Dim Rad_All = radTD_All 'All radio button

            'Corresponding Names for each radio button
            Dim names(6, 2) As String
            names(1, 1) = "_A1"
            names(2, 1) = "_A2"
            names(3, 1) = "_A3"
            names(4, 1) = "_A4"
            names(5, 1) = "_A5"
            names(6, 1) = "_A6"
            names(1, 2) = "_B1"
            names(2, 2) = "_B2"
            names(3, 2) = "_B3"
            names(4, 2) = "_B4"
            names(5, 2) = "_B5"
            names(6, 2) = "_B6"
            Dim name_dum As String = "_Dum" 'Dummy name
            'No suffix for "All"

            Dim c As Byte = 1               'Column iterator
            Dim r As Byte = 1               'Row iterator
            Dim result As Boolean = False   'Result escape flag

            'Iterate through all columns then rows, until find a checked button; then escape
            For c = 1 To 2
                For r = 1 To 6
                    result = Rads(r, c).Checked
                    If result = True Then Exit For
                Next r
                If result = True Then Exit For
            Next c

            'If no results, then check dummy radio button
            If result = False Then
                If Rad_Dum.Checked = True Then nameResult = name_dum 'If dummy checked, set to dummy cell name
                If nudTD_M.Value <> 0 Then suffix = "_" & TryParseErr_Byte(nudTD_M.Value)
                'If check fails, assume "All" button, which is null cell name
            Else
                'If a normal array result was found, fetch appropriate name
                nameResult = names(r, c)
                If nudTD_M.Value <> 0 Then suffix = "_" & TryParseErr_Byte(nudTD_M.Value)
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in GetCellFrameName()!")
        End Try
        nameResult = nameResult & suffix
        Return nameResult
    End Function

    ''' <summary>
    ''' Given a numeric text string, keeps it within range between min and max values
    ''' </summary>
    ''' <param name="inp">Numeric string</param>
    ''' <param name="min">Min value</param>
    ''' <param name="max">Max value</param>
    ''' <returns>Numeric string in range</returns>
    ''' <remarks></remarks>
    Private Function KeepInRange(ByVal inp As String, ByVal min As UShort, ByVal max As UShort) As String
        Dim outp As String = ""                             'Output
        Try
            Dim val As UShort = TryParseErr_UShort(inp)     'Get numeric value of input
            Dim newVal As UShort = 0                        'New value to apply

            'Keep newval between min and max, and if outside, use either newval=min or max appropriately
            'If in range, newval=val
            If val < min Then
                newVal = min
            ElseIf val > max Then
                newVal = max
            Else
                newVal = val
            End If
            outp = newVal.ToString()                        'Set output as strinng of newval
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in KeepInRange func!")
        End Try
        Return outp
    End Function



    '===========================
    'THP Encoder group box

    ''' <summary>
    ''' Handles encoding many input subvideos, a wav file, and dummy padding frames into a composite THP file.
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks>This the main feature of the program, and quite schmancy</remarks>
    Private Sub btnTE_Enc_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnTE_Enc.Click
        'Psuedo code of encoding process. Assume an array of subvideos with a multiplicity.
        'During these steps, MP4s with an -avcodec of h264 are used, in order to preserve loseless but compressed quality output during the multiple passes.
        'Ideally, I would use AVIs with -avcodec of rawvideo, but AVIs don't support video dimensions that aren't a mult of 16 without speedloss

        '0.  If video has padding, convert the appropriate dummy bmp file ("dummy_N.bmp") to a video of appropriate frame length ("dummy_N.mp4") for the current multiplicity
        '1.  All subvideos in a column are vstacked ("cN.mp4"). Do for all columns
        '2.  ALl subvideos in a column are then limited to F frames ("dN.mp4"). Do for all columns
        '3.  HStack all frame-limited column videos ("dN.mp4" in step 2) to create a composite video with all subvideos included for the current multiplicity. ("mN.mp4", where N is the current multiplicity)
        '4.  Repeat steps 0-3 for each multiplicity
        '5.  Concatenate each composite multiplicity video (all "mN.mp4" files in step 3) to a nearly-final mp4 file ("filename.mp4")
        '6.  If video has padding, concatenate all dummy video multiplicities ("dummy_N.mp4" in step 0) to a composite dummy video ("dummy.mp4")
        '7.  If video has padding, vstack the video in step 5 ("filename.mp4") with the composite dummy
        '    video in step 6 ("dummy.mp4") into a file called "final.mp4".
        '    MoveFile "final.mp4"->"filename.mp4"                
        '7.2 Convert filename.mp4 to yuv420p format
        '8.  Convert final video ("filename.mp4") into BMP frames, padded to N digits ("frame_%0Nd.bmp")
        '8.1 Find, copy, and hack Irfanview advanced options INI file
        '8.2 Convert BMP frames into JPG frames, using irfanview, and the JPG Quality value
        '9.  Check the output directory, and delete any extra jpg frames past the framelimit (frames * m).
        '10. The jpg files and the audio file (if applicable, "filename.wav") are converted into "filename.thp" with THPConv at proper framerate
        '11. Cleanup() is run to delete all temporary files from steps 0-10 during the conversion
        '12. Done!

        'Imagine having to do the above steps manually with specially-crafted batch scripts
        'for the specific configuration of each THP file you want encoded,
        ' or worse, without scripts and with a video edtior x_x!

        'Thwimp tackles this proplem headon, and in a very automated fashion!

        'Naming conventions:
        'The working directory for conversion needs the following input files:
        '*  MP4 video files for each subvideo, and for each multiplicity. These MP4s should be encoded with -vcodec h264
        '   Named as "filename_AX_Y.mp4", where "A" is a letter indicating the row ID in the array,
        '   where "X" is the column ID as a number, and "Y" is the multiplicity ID. A and X are setup in MS Excel A1N notation
        '*  If video has audio, "filename.wav" for the audio stream
        '*  If video has dummy padding, a single BMP image file for each multiplicity
        '   ("dummy_N.bmp", where N is the current mult ID).
        '   This will be converted into a video of fixed framelength and used during the processing

        'In the THP Encoding group box, an array of checkboxes indicates what files will be needed
        'to fulfill the array, and also if a file will be needed for padding or audio

        'The THP Encoding group box has 2 user inputs:
        '*"Trunc Frame" - Amount of frames to truncate subvideos to.
        '   This is used to ensure all subvideos have the same framelength
        '   (e.g., if bad video mastering made, say 255 frames vs. target 250 for all videos)
        '*"Digs"- The amount of digits in the "Trunc Frame" value.
        '   Used for some filenaming formating.
        ' They should match appropriately!

        'BEGIN CODE!

        'FFMPEG NOTES:
        'Vertical and horizontal stacking (2 files)
        'https://unix.stackexchange.com/questions/233832/merge-two-video-clips-into-one-placing-them-next-to-each-other
        'ffmpeg -i top.mp4 -i bot.mp4 -filter_complex vstack output.mp4
        'ffmpeg -i left.mp4 -i right.mp4 -filter_complex hstack output.mp4

        'V/HStacking for N videos
        'https://stackoverflow.com/questions/11552565/vertically-or-horizontally-stack-several-videos-using-ffmpeg/33764934#33764934
        'ffmpeg -i input0 -i input1 -i input2 -filter_complex "[0:v][1:v][2:v]vstack=inputs=3[v]" -map "[v]" output

        'Concatenate N videos. This usually requires using -i ListOfFiles.txt to work properly :(.
        'https://stackoverflow.com/questions/7333232/how-to-concatenate-two-mp4-files-using-ffmpeg
        'ffmpeg -i opening.mkv -i episode.mkv -i ending.mkv -filter_complex concat output.mkv"

        'Show ofdOutput, let user select the working directory with the input files
        Try
            If ofdOutput.ShowDialog() = Windows.Forms.DialogResult.Cancel Then Exit Sub

            Dim startInfo As ProcessStartInfo
            startInfo = New ProcessStartInfo
            startInfo.UseShellExecute = False
            Dim shell As Process
            shell = New Process

            Dim path As String = ofdOutput.SelectedPath         'The working directory with our input files.
            Dim filename As String = FileAndExt(cmbTHP.Text)    '"filename.thp" we want to create
            Dim file As String = ""                             'Generic file string
            Dim file2 As String = ""                            'Generic file string2
            filename = filename.Replace(".thp", "")             'Remove extension from filename var
            Dim cmd As String = ""                              'String with commands to run in a Process/Shell

            'Generic iterators
            Dim i As UShort = 1   'Usually rows
            Dim j As UShort = 1   '~       cols
            Dim k As Byte = 1   '~       multiplicity
            Dim cnt As UShort = 0

            'THP Array dims        
            Dim r As Byte = TryParseErr_Byte(txtArr_R.Text)         'Amount of rows
            Dim c As Byte = TryParseErr_Byte(txtArr_C.Text)         'Amount of cols
            Dim m As Byte = TryParseErr_Byte(txtVM_M.Text)          'Amount of mult
            Dim suffix As String                                    'The suffix to use to meet array naming conventions        

            Dim parms(6) As String                                  'Array of generic string parameters for cmd string building. Usually used for v/hstacking N videos.
            Dim parm As String                                      'Usually the concatenation of the elements in the parms array
            Dim frames As UShort = TryParseErr_UShort(txtTE_F.Text) 'The amount of frames to limit each subvideo to
            Dim FPS As Single = TryParseErr_Single(txtVC_F.Text)    'The framerate FPS as single

            'Array of suffixes for the naming conventions in MS Excel A1N notation (Row, Column)
            'It is hardcoded to 6x2, since the components of each length dimension don't go any larger than this
            Dim suffixes(6, 2) As String
            suffixes(1, 1) = "_A1"
            suffixes(2, 1) = "_A2"
            suffixes(3, 1) = "_A3"
            suffixes(4, 1) = "_A4"
            suffixes(5, 1) = "_A5"
            suffixes(6, 1) = "_A6"
            suffixes(1, 2) = "_B1"
            suffixes(2, 2) = "_B2"
            suffixes(3, 2) = "_B3"
            suffixes(4, 2) = "_B4"
            suffixes(5, 2) = "_B5"
            suffixes(6, 2) = "_B6"

            Dim Files(6) As String          'Array of input files for ffmpeg. Written to File.txt, used as input listing file
            Dim hasPad As Boolean = THPHasPad()

            CleanUp(path, filename, r, c, m, hasPad, False)         'Cleanup leftover temp files at working dir

            'BEGIN PROCESSING
            'Iterate through all multiplicities from 1 to m
            For k = 1 To m Step 1

                If hasPad Then
                    'Do Step 0 if padding
                    Dim dg As Byte = frames.ToString().Length   'The amount of frames to limit to in digits
                    Dim dgs As String = StrDup(dg, "0")         'A .ToString() format string, limiting to N digits
                    cnt = 0                       'Generic iterator

                    'Convert dummy still images for the current multiplicity to a video.
                    'Do this by copying the image to many sequentially named files,
                    'then render all frames as .mp4 video

                    'Iterate through all frames from 1 to Frames
                    For cnt = 1 To frames
                        file = path & strBAK & "dummy_" & k.ToString() & ".bmp"                             'file =     "C:\WorkingDir\dummy_N.bmp"
                        file2 = path & strBAK & "dummy_" & k.ToString() & "_" & cnt.ToString(dgs) & ".bmp"  'file2 =    "C:\WorkingDir\dummy_N_FFF.bmp"
                        My.Computer.FileSystem.CopyFile(file, file2)                                        'Copy file to file2
                    Next cnt

                    'Convert bmp files to MP4: ffmpeg -f image2 -i image_%03d.bmp test.mp4
                    cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y"                                    '"C:\FFMPegPath\FFMPeg.exe" -y                    
                    cmd &= " -f image2"                                                                                     ' -f image2

                    file = strQUOT & path & strBAK & "dummy_" & k.ToString() & "_%0" & dg.ToString() & "d.bmp" & strQUOT
                    cmd &= " -i " & file                                                                                    ' -i "C:\WorkingDir\dummy_M_%0Nd.bmp"

                    file = strQUOT & path & strBAK & "dummy_" & k.ToString() & ".mp4" & strQUOT                             ' -fm  -vcodec h264 -framerate RR.RR & C:\Path\dummy_N.pm4
                    cmd &= " -vcodec h264 -framerate " & FPS & " " & file

                    'Run cmd
                    startInfo.FileName = cmd
                    shell.StartInfo = startInfo
                    shell.Start()
                    shell.WaitForExit()

                    'Cleanup all of the BMP frames
                    CleanUp(path, filename, r, c, m, hasPad, True)
                End If
                shell.Close()

                'Do step 1
                'Iterate through columns 1 to C
                For j = 1 To c Step 1
                    'ffmpeg -i input0 -i input1 -i input2 -filter_complex "[0:v][1:v][2:v]vstack=inputs=3[v]" -map "[v]" output

                    parm = ""                                                                                                       'Clear parm string
                    ReDim parms(r)                                                                                                  'Redim parm array to the amount of rows
                    cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y "                                           '"C:\FFMPegPath\FFMPeg.exe" -y

                    'Iterate through all rows 1 to r
                    'Concatenate all input file args ("-i filename") onto cmd, build input pads
                    For i = 1 To r Step 1
                        suffix = suffixes(i, j) & "_" & k.ToString()                                                                'Get appropriate video cell suffix ("_AX_Y")
                        file = strQUOT & path & strBAK & filename & suffix & ".mp4" & strQUOT
                        cmd &= "-i " & file                                                                                         '-i "C:\WorkingDir\filename_AX_Y.mp4"
                        cmd &= " "
                        parms(i) = "[" & (i - 1).ToString() + ":v]"                                                                 'Generate input pad for element in array ("[N:v]")
                        parm &= parms(i)                                                                                            'Concatenate index onto parm
                    Next i

                    file = strQUOT & path & strBAK & "c" & j.ToString() & ".mp4" & strQUOT                                          'Filename for output column video ("cN.mp4"). "C:\WorkingDir\cN.mp4"

                    If r > 1 Then
                        'If multiple rows
                        '-filter_complex "([0:v] to [r:v])vstack=inputs=r[v]" -map "[v]" -vcodec h264 "C:\WorkingDir\cN.mp4"
                        cmd &= "-filter_complex " & strQUOT
                        cmd &= parm & "vstack=inputs=" & r.ToString() & "[v]" & strQUOT & " -map " & strQUOT & "[v]" & strQUOT
                        cmd &= " -vcodec h264 " & file
                    Else
                        'If one row, just set output to "C:\WorkingDir\cN.mp4"
                        'Final cmd will be
                        '"C:\FFMPegDir\ffmpeg.exe" -y -i "C:\WorkingDir\title_AX_Y.mp4" -vcodec h264 "C:\WorkingDir\cN.mp4"
                        cmd &= "-vcodec h264 " & file
                    End If

                    'Run cmd
                    startInfo.FileName = cmd
                    shell.StartInfo = startInfo
                    shell.Start()
                    shell.WaitForExit()
                Next j
                shell.Close()

                'Do Step 2
                'Iterate through columns 1 to C
                For j = 1 To c Step 1
                    cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y "           '"C:\FFMPegDir\FFMPeg.exe" -y
                    file = strQUOT & path & strBAK & "c" & j.ToString() & ".mp4" & strQUOT
                    cmd &= "-i " & file                                                             '-i "C:\WorkingDir\cN.mp4"
                    file = strQUOT & path & strBAK & "d" & j.ToString() & ".mp4" & strQUOT
                    'End frame is exclusive; add one to frame count for value to use
                    cmd &= " -filter_complex trim=start_frame=0:end_frame=" & (TryParseErr_UShort(frames) + 1).ToString() & " -vcodec h264 " & file   ' -filter_complex trim=start_frame=X:end_frame=Y -vcodec h264 "C:\WorkingDir\dN.mp4"

                    '"-filter complex trim=start_frame=X:end_frame=Y" only renders frames X-Y for a video
                    'Run cmd
                    startInfo.FileName = cmd
                    shell.StartInfo = startInfo
                    shell.Start()
                    shell.WaitForExit()
                Next j
                shell.Close()

                'Do Step 3
                parm = ""                                                               'Clear parm string
                ReDim parms(c)                                                          'ReDim parms to amount of columns
                cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y "   '"C:\FFMPegDir\FFMPeg.exe" - y
                'Iterate through all columns 1 to c
                'Concatenate all input file args ("-i dN.mp4") onto cmd, build input pads. Similar to Step 1 with vstack
                For j = 1 To c Step 1
                    file = strQUOT & path & strBAK & "d" & j.ToString() & ".mp4" & strQUOT
                    cmd &= "-i " & file
                    cmd &= " "
                    parms(j) = "[" & (j - 1).ToString() + ":v]"
                    parm &= parms(j)
                Next j
                file = strQUOT & path & strBAK & "m" & k.ToString() & ".mp4" & strQUOT

                If c > 1 Then
                    'If multiple columns
                    '-filter_complex "([0:v] to [c:v])hstack=inputs=c[v]" -map "[v]" -vcodec h264 "C:\WorkingDir\mN.mp4"
                    cmd &= "-filter_complex " & strQUOT
                    cmd &= parm & "hstack=inputs=" & c.ToString() & "[v]" & strQUOT & " -map " & strQUOT & "[v]" & strQUOT
                    cmd &= " -vcodec h264 " & file
                Else
                    'If one col, just set output to "C:\WorkingDir\mN.mp4"
                    'Final cmd will be
                    '"C:\FFMPegDir\ffmpeg.exe" -y -i "C:\WorkingDir\d1.mp4" -vcodec h264 "C:\WorkingDir\mN.mp4"
                    cmd &= " -vcodec h264 " & file
                End If

                'Run cmd
                startInfo.FileName = cmd
                shell.StartInfo = startInfo
                shell.Start()
                shell.WaitForExit()
                shell.Close()
            Next k  'Do Step 4

            'Do Step 5
            'https://stackoverflow.com/questions/5415006/ffmpeg-combine-merge-multiple-mp4-videos-not-working-output-only-contains-the
            'ffmpeg -f concat -i inputs.txt -vcodec h264 Mux1.mp4
            If m > 1 Then
                'If video has multiplicity

                '"C:\FFMPegDir\FFMPeg.exe" -y -f concat -i
                cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y -f concat -i "

                'Redim files to 0-based multiplicity
                ReDim Files(m - 1)
                'Iterate multiplicity from 1 to m
                For k = 1 To m Step 1
                    '0-based file index = "mN.mp4", where "N" is 1-based
                    Files(k - 1) = "m" & k.ToString() & ".mp4"
                Next k
                WriteTxtFile(path, Files)                                                                           'Write file list (File.txt) to WorkingDir
                file = strQUOT & path & strBAK & "File.txt" & strQUOT                                               'That file is located at "C:\WorkingDir\File.Txt"
                cmd &= file & " -vcodec h264 " & strQUOT & path & strBAK & filename & ".mp4" & strQUOT '"C:\WorkingDir\File.Txt" -vcodec h264 "C:\WorkingDir\filename.mp4"

                'Run cmd
                startInfo.FileName = cmd
                startInfo.WorkingDirectory = path
                shell.StartInfo = startInfo
                shell.Start()
                shell.WaitForExit()
                shell.Close()
            Else
                'If video has no multiplicity, just copy "C:\WorkingDir\m1.mp4" to "C:\WorkingDir\filename.mp4"
                file = path & strBAK & "m1.mp4"             '"C:\WorkingDir\m1.mp4"
                file2 = path & strBAK & filename & ".mp4"   '"C:\WorkingDir\filename.mp4"
                My.Computer.FileSystem.CopyFile(file, file2)
            End If

            'If we have dummy padding, concatenate each of the dummy_*.mp4 files into dummy.mp4,
            'then vstack filename.mp4 with dummy.mp4 for final.mp4. Rename final.mp4 to filename.mp4 and replace
            If hasPad Then
                'If padding, Do Step 6

                If m > 1 Then
                    'If multiplicity, concatenate all dummy_N.mp4 to dummy.mp4

                    'Setup similar to Step 5
                    '"C:\FFMPegDir\FFMPeg.exe" -y -f concat -i
                    cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y -f concat -i "
                    'Redim files to 0-based multiplicity
                    ReDim Files(m - 1)
                    'Iterate multiplicity from 1 to m
                    For k = 1 To m Step 1
                        '0-based file index = "dummy_N.mp4", where "N" is 1-based
                        Files(k - 1) = "dummy_" & k.ToString() & ".mp4"
                    Next k
                    WriteTxtFile(path, Files)                                                                       'Write file list (File.txt) to WorkingDir
                    file = strQUOT & path & strBAK & "File.txt" & strQUOT                                           'That file is located at "C:\WorkingDir\File.Txt"
                    cmd &= file & " -vcodec h264 " & strQUOT & path & strBAK & "dummy.mp4" & strQUOT   '"C:\WorkingDir\File.Txt" -vcodec h264 "C:\WorkingDir\dummy.mp4"

                    'Run cmd
                    startInfo.FileName = cmd
                    startInfo.WorkingDirectory = path
                    shell.StartInfo = startInfo
                    shell.Start()
                    shell.WaitForExit()
                    shell.Close()
                Else
                    'If no multiplicity, copy "C:\WorkingDir\dummy_1.mp4" to "C:\WorkingDir\dummy.mp4"
                    file = path & strBAK & "dummy_1.mp4"
                    file2 = path & strBAK & "dummy.mp4"
                    My.Computer.FileSystem.MoveFile(file, file2, True)
                End If

                'Do Step 7
                'vstack filename.mp4 with dummy.mp4 into final.mp4
                'ffmpeg -i top.mp4 -i bot.mp4 -filter_complex vstack output.mp4
                cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y"    '"C:\FFMPegDir\FFMPeg.exe" -y
                file = strQUOT & path & strBAK & filename & ".mp4" & strQUOT
                cmd &= " -i " & file                                                    ' -i "C:\WorkingDir\filename.mp4"
                file = strQUOT & path & strBAK & "dummy.mp4" & strQUOT

                cmd &= " -i " & file
                cmd &= " -filter_complex vstack -vcodec h264 "
                file = strQUOT & path & strBAK & "final.mp4" & strQUOT
                cmd &= file                                                             ' -i "C:\WorkingDir\dummy.mp4 -filter_complex vstack -vcodec h264 "C:\WorkingDir\final.mp4""

                'Run cmd
                startInfo.FileName = cmd
                shell.StartInfo = startInfo
                shell.Start()
                shell.WaitForExit()
                shell.Close()

                'MoveFile("C:\WorkingDir\final.mp4"->"C:\WorkingDir\filename.mp4")
                file = path & strBAK & "final.mp4"
                file2 = path & strBAK & filename & ".mp4"
                My.Computer.FileSystem.MoveFile(file, file2, True)
            End If

            'Do Step 7.2: Convert filename.mp4 to yuv420p format
            'cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y"    '"C:\FFMPegDir\FFMPeg.exe" -y 
            'file = strQUOT & path & strBAK & filename & ".mp4" & strQUOT            ' -i "C:\WorkingDir\filename.mp4"
            'cmd &= " -i " & file
            'file = strQUOT & path & strBAK & "final.mp4" & strQUOT                  ' -pix_fmt yuvj420p -vcodec h264 "C:\WorkingDir\final.mp4"
            'cmd &= " -pix_fmt yuvj420p -vcodec h264 " & file
            'Run cmd
            'startInfo.FileName = cmd
            'shell.StartInfo = startInfo
            'shell.Start()
            'shell.WaitForExit()
            'shell.Close()

            'MoveFile C:\WorkingDir\final.mp4 -> C:\WorkingDir\filename.mp4
            'file = path & strBAK & "final.mp4"                      'C:\WorkingDir\final.mp4
            'file2 = path & strBAK & filename & ".mp4"               'C:\WorkingDir\filename.mp4
            'My.Computer.FileSystem.MoveFile(file, file2, True)


            'Do Step 8: Output to .bmp frames
            i = TryParseErr_Byte(txtTE_D.Text)                                              'Set i to amount of digits in framelimit
            cmd = strQUOT & txtFFMpeg.Text & strBAK & exeFMPeg & strQUOT & " -y "           '"C:\FFMPegDir\FFMPeg.exe" -y 
            file = strQUOT & path & strBAK & filename & ".mp4" & strQUOT
            cmd &= "-i " & file                                                             '-i "C:\WorkingDir\filename.mp4"
            file = strQUOT & path & strBAK & "frame_%0" & i.ToString() & "d.bmp" & strQUOT
            cmd &= " " & file                                                               ' "C:\WorkingDir\frame_%0Nd.bmp"
            'Run cmd
            startInfo.FileName = cmd
            shell.StartInfo = startInfo
            shell.Start()
            shell.WaitForExit()
            shell.Close()

            'Do Step 8.1: Hack INI file, throw error if failure
            Dim success As Boolean = HackINIFile(path)
            If success = False Then Throw New System.Exception("Irfanview INI hack failed!")

            'Do Step 8.2: Convert .bmp frames to .jpg frames
            MsgBox("Generating JPG frames; please wait!", MsgBoxStyle.Information, "JPG Rendering")
            cnt = TryParseErr_Byte(txtTE_D.Text)                                                    'Get amount of digits            
            j = CountFilesFromFolder(path, "frame_*.bmp")                                           'Count amount of BMP frames
            For i = 1 To j                                                                          'Iterate frames from 1 to j
                cmd = strQUOT & txtiView.Text & strQUOT                                             '"C:\iView32\iView32.exe"
                file2 = StrDup(cnt, "0")                                                            '"0Nd". Create ToString dig formatter
                file = strQUOT & path & strBAK & "frame_" & i.ToString(file2) & ".bmp" & strQUOT
                cmd &= " " & file                                                                   ' "C:\WorkingDir\frames_%0Nd.bmp
                'cmd &= " /jpgq=" & nudTE_jpgq.Value.ToString() & " /convert="                      ' /jpgq=N /convert-
                cmd &= " /ini /convert="                                                            '/ini /convert=
                file = strQUOT & path & strBAK & "frame_" & i.ToString(file2) & ".jpg" & strQUOT
                cmd &= file                                                                   ' "C:\WorkingDir\frames_%0Nd.jpg

                'Run cmd
                startInfo.FileName = cmd
                shell.StartInfo = startInfo
                shell.Start()
                shell.WaitForExit()
            Next i
            shell.Close()

            'Do Step 9
            file = "frame_*.jpg"
            DeleteExtraFilesFromFolder(path, file, frames * m)
            file = "frame_*.bmp"
            DeleteFilesFromFolder(path, file)

            'Do Step 10
            Dim hasAudio As Boolean = THPHasAudio()
            If hasAudio = False Then
                'If no audio, just convert jpg frames into THP using THPConv at appropriate framerate
                '"C:\THPConvDir\THPConv.exe" -j "C:\WorkingDir\*.jpg" -r RR.RR -d "C:\WorkingDir\filename.thp"
                cmd = strQUOT & txtTHPConv.Text & strQUOT
                file = "-j " & strQUOT & path & strBAK & "*.jpg" & strQUOT
                cmd &= " " & file
                cmd &= " -r " & FPS.ToString("F2")
                file = strQUOT & path & strBAK & filename & ".thp" & strQUOT
                cmd &= " -d " & file
            Else
                'If audio, convert jpg frames and add audio file at appropriate framerate
                '"C:\THPConvDir\THPConv.exe" -j "C:\WorkingDir\*.jpg" -s "C:\WorkingDir\filename.wav" -r RR.RR -d "C:\WorkingDir\filename.thp"
                cmd = strQUOT & txtTHPConv.Text & strQUOT
                file = "-j " & strQUOT & path & strBAK & "*.jpg" & strQUOT
                cmd &= " " & file
                file = strQUOT & path & strBAK & filename & ".wav" & strQUOT
                cmd &= " -s " & file
                cmd &= " -r " & FPS.ToString("F2")
                file = strQUOT & path & strBAK & filename & ".thp" & strQUOT
                cmd &= " -d " & file
            End If

            'Run cmd
            startInfo.FileName = cmd
            shell.StartInfo = startInfo
            shell.Start()
            shell.WaitForExit()
            shell.Close()

            'Step 11
            MsgBox("THP rendered! Now cleaning up...", MsgBoxStyle.Information, "Success!")
            CleanUp(path, filename, r, c, m, hasPad, False)

            'Step 12: Done!
            MsgBox("Done!", MsgBoxStyle.Information, "Tada!")
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error during Encoding!")
        End Try
    End Sub

    ''' <summary>
    ''' Cleans up temporary files during THP encoding
    ''' </summary>
    ''' <param name="Path">Path to working dir</param>
    ''' <param name="filename">Filename for the THP</param>
    ''' <param name="r">Amount of rows in subvideo array</param>
    ''' <param name="c">Amount of cols in subvideo array</param>
    ''' <param name="m">Multiplicity for subvids</param>
    ''' <param name="Haspad">Does video use padding?</param>
    ''' <param name="justBMPs">Just cleanup bmps for dummy video encoding?</param>
    ''' <remarks></remarks>
    Private Sub CleanUp(ByVal Path As String, ByVal filename As String, ByVal r As Byte, ByVal c As Byte, ByVal m As Byte, ByVal Haspad As Boolean, ByVal justBMPs As Boolean)
        Try
            'Generic iterators
            'Dim i As Byte       'Rows
            Dim j As Byte       'Cols
            Dim k As Byte       'Multiplicity
            Dim File As String  'File to check against/to delete/to whatever

            If justBMPs = False Then
                'Delete thpfilename.mp4 (final mp4 without padding) if exists
                File = Path & strBAK & filename & ".mp4"
                If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)

                'Delete column videos
                'Iterate cols from 1 to c
                For j = 1 To c Step 1
                    'if cN.mp4 or dN.mp4 exists, delete
                    File = Path & strBAK & "c" & j.ToString() & ".mp4"
                    If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)
                    File = Path & strBAK & "d" & j.ToString() & ".mp4"
                    If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)
                Next j

                'Delete multiplicity videos
                'Iterate mult from 1 to m
                For k = 1 To m Step 1
                    'if mN.mp4 exists, delete
                    File = Path & strBAK & "m" & k.ToString() & ".mp4"
                    If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)
                Next k

                'Delete all jpg frames (used for THP video stream)
                DeleteFilesFromFolder(Path, "*.jpg")
            End If

            'If video has padding, delete temp files for dummy padding
            If Haspad Then
                'Delete BMP frames from dummy videos (dummy_N_%0Nd_.bmp)
                'Iterate multiplicity from 1 to m
                For k = 1 To m Step 1
                    Dim srch As String = "dummy_" & k.ToString() & "_*.bmp"
                    DeleteFilesFromFolder(Path, srch)
                Next k

                If justBMPs = False Then
                    'Delete dummy_N.mp4 files
                    DeleteFilesFromFolder(Path, "dummy*.mp4")

                    'Delete final.mp4 if exists
                    File = Path & strBAK & "final.mp4"
                    If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)
                End If
            End If

            If justBMPs = False Then
                'Delete file.txt if exists, a list of files used for -i in ffmpeg.exe
                File = Path & strBAK & "File.txt"
                If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)

                'Also delete Irfanview JPG INI files
                File = Path & strBAK & "i_view32.ini"
                If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)
                File = Path & strBAK & "i_view32_temp.ini"
                If System.IO.File.Exists(File) Then My.Computer.FileSystem.DeleteFile(File)
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in CleanUp!")
        End Try
    End Sub

    'Update the THP Encoder digits on leav/change of digits textbox
    Private Sub txtTE_F_Leave(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTE_F.Leave
        UpdateTEDigs()
    End Sub
    Private Sub txtTE_F_TextChange(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles txtTE_F.TextChanged
        UpdateTEDigs()
    End Sub
    ''' <summary>
    ''' Auto-updates the THP Encoder digits box for the 0-padding for the output JPEG frames.
    ''' Based on the amount of frames to limit each subvideo * multiplicity
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub UpdateTEDigs()
        Try
            Dim m As Byte = TryParseErr_Byte(txtVM_M.Text)          'The multiplicity in the THP
            Dim cnt As UShort = TryParseErr_UShort(txtTE_F.Text)    'The amount of frames to limit each subvideo
            Dim max As UShort = cnt * m                             'Max amount of frames in new THP
            Dim digs As Byte = max.ToString().Length                'Get the amount of digits in the max value
            txtTE_D.Text = digs.ToString()                          'Update the digits text
        Catch ex As Exception
            'MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in UpdateTEDigs()!")
        End Try
    End Sub

    'If any of the checkboxes in the THP_Enc group box array have been changed, maintain the current state for the THP file.
    Private Sub chkTE_A1_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_A1.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_A2_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_A2.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_A3_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_A3.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_A4_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_A4.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_A5_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_A5.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_A6_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_A6.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_B1_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_B1.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_B2_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_B2.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_B3_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_B3.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_B4_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_B4.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_B5_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_B5.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_B6_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_B6.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_Dum_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_Dum.CheckedChanged
        HandleArrState()
    End Sub
    Private Sub chkTE_wav_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTE_wav.CheckedChanged
        HandleArrState()
    End Sub

    ''' <summary>
    ''' Handles the checkbox array depiction of naming conventions for THP encoding, and radio buttons for cells for THP Decoding
    ''' Also updates multiplicity NUD for ripping (time)
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub HandleArrState()
        'Encoding array
        Dim Enc_Boxes(6, 2) As System.Windows.Forms.CheckBox    'Array of 6x2 check boxes for THP encoding
        Dim Enc_Dum As System.Windows.Forms.CheckBox            'Dummy check box (for padding)
        Dim Enc_Wav As System.Windows.Forms.CheckBox            'Wav check box (for audio wav file)

        'Decoding array
        Dim Dec_Rads(6, 2) As System.Windows.Forms.RadioButton  'Array of 6x2 radio buttons for THP decoding
        Dim Dec_Dum As System.Windows.Forms.RadioButton         'Dummy radio button (for padding)
        Dim Dec_All As System.Windows.Forms.RadioButton         'Radio button for all

        'Init the encoding array. In A1N MS Excel notation, Alpha=row, Number=Col
        Enc_Boxes(1, 1) = chkTE_A1
        Enc_Boxes(2, 1) = chkTE_A2
        Enc_Boxes(3, 1) = chkTE_A3
        Enc_Boxes(4, 1) = chkTE_A4
        Enc_Boxes(5, 1) = chkTE_A5
        Enc_Boxes(6, 1) = chkTE_A6
        Enc_Boxes(1, 2) = chkTE_B1
        Enc_Boxes(2, 2) = chkTE_B2
        Enc_Boxes(3, 2) = chkTE_B3
        Enc_Boxes(4, 2) = chkTE_B4
        Enc_Boxes(5, 2) = chkTE_B5
        Enc_Boxes(6, 2) = chkTE_B6
        'Wav and dummy boxes
        Enc_Dum = chkTE_Dum
        Enc_Wav = chkTE_wav

        'Init the decoding array. In A1N MS Excel notation, Alpha=row, Number=Col
        Dec_Rads(1, 1) = radTD_A1
        Dec_Rads(2, 1) = radTD_A2
        Dec_Rads(3, 1) = radTD_A3
        Dec_Rads(4, 1) = radTD_A4
        Dec_Rads(5, 1) = radTD_A5
        Dec_Rads(6, 1) = radTD_A6
        Dec_Rads(1, 2) = radTD_B1
        Dec_Rads(2, 2) = radTD_B2
        Dec_Rads(3, 2) = radTD_B3
        Dec_Rads(4, 2) = radTD_B4
        Dec_Rads(5, 2) = radTD_B5
        Dec_Rads(6, 2) = radTD_B6
        'Dummy and all chks
        Dec_Dum = radTD_Dum
        Dec_All = radTD_All

        'Generic iterators
        Dim i As Byte = 0
        Dim j As Byte = 0

        'Handle Encoding stuff
        Try
            'Update the checked and enabled states of the array based on the video data 
            'Amount of rows in video, columns, and multiplicity            
            Dim r As Byte = TryParseErr_Byte(txtArr_R.Text)
            Dim c As Byte = TryParseErr_Byte(txtArr_C.Text)
            Dim m As Byte = TryParseErr_Byte(txtVM_M.Text)
            Dim state As Boolean = False                    'Generic bool

            For i = 1 To 6 Step 1                           'Iterate through all rows (1-6)
                For j = 1 To 2 Step 1                       'Iterate through all cols (1-2)
                    If r = 0 And c = 0 Then
                        'If a dummy entry in combo box was selected, then r & c of array will be 0. Set all chks/boxes to unchecked/disabled
                        state = False
                    Else
                        If i <= r And j <= c Then
                            'If i and j iterators (row/col) are within
                            'the amount of rows and col for this video, 
                            'then cell is used. Check & enable
                            state = True
                        Else
                            'Otherwise unused, uncheck and disable
                            state = False
                        End If
                    End If

                    'Update the checked/enabled states as appropriately for the Enc boxes
                    Enc_Boxes(i, j).Checked = state
                    Enc_Boxes(i, j).Enabled = state

                    'Always set the radio buttons as unchecked
                    Dec_Rads(i, j).Checked = False
                    Dec_Rads(i, j).Enabled = state
                Next j
            Next i
            '"All radio" button is always enabled and checked by default
            Dec_All.Checked = True
            Dec_All.Enabled = True


            'Handle dummy checkbox/chk states
            state = THPHasPad()
            Enc_Dum.Checked = state
            Enc_Dum.Enabled = state
            Dec_Dum.Checked = False 'Dummy is never set by default
            Dec_Dum.Enabled = state

            'Handle wav checkbox states
            state = BoolStrToBool(txtA_A.Text)    'If "True" then true
            'Update the wav checkbox states
            Enc_Wav.Checked = state
            Enc_Wav.Enabled = state

            'Handle the multiplicity box (the m values), and mult for time ripping
            'If only m=1, then "_1", else "_1\nto\nM"
            If m = 1 Then
                txtTE_M.Text = "_1"

                'Set range to 0 to 1. 0=all frames (no suffixes), 1=only frame (_1 suffix, for naming convention nitpickery)
                nudTD_M.Minimum = 0
                nudTD_M.Maximum = 1
            Else
                'Update the text multi box
                txtTE_M.Text = "_1" & strNL & "to" & strNL & "_" & m.ToString()
                txtTE_F.Text = txtVF_S.Text

                'Update the time rip NUD
                nudTD_M.Minimum = 0
                nudTD_M.Maximum = m
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in HandleArrState()!")
        End Try
    End Sub



    '===========================
    'DOS file path functions

    ''' <summary>
    ''' Given a full file path, returns the directory
    ''' </summary>
    ''' <param name="strPath">Full file path</param>
    ''' <returns>File path</returns>
    ''' <remarks></remarks>
    Public Function FileDir(ByVal strPath As String) As String
        Dim strOut As String = ""       'Output
        Try
            Dim strFile As String = ""      'The file itself (ie, File.ext)        
            strFile = FileAndExt(strPath)   'Get the file+extension

            'From the full file path, replace the file+ext with nothing, to get file directory; return
            strOut = Replace(strPath, strFile, "")
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in FileDir()!")
        End Try
        Return strOut
    End Function

    ''' <summary>
    ''' Given a full file path, returns filepath with changed file extension
    ''' </summary>
    ''' <param name="strPath">Full file path</param>
    ''' <param name="strOldExt">Old extension</param>
    ''' <param name="strNewExt">New extension</param>
    ''' <returns>Full file with new extension</returns>
    ''' <remarks></remarks>
    Public Function FileChangeExt(ByVal strPath As String, ByVal strOldExt As String, ByVal strNewExt As String)
        'Get the file+ext from the file path, replace old extension with new extension
        FileChangeExt = Replace(FileAndExt(strPath), strOldExt, strNewExt)
    End Function

    ''' <summary>
    ''' Given a full file path, returns the filename+ext
    ''' </summary>
    ''' <param name="strPath">Full file path</param>
    ''' <returns>Filename+ext</returns>
    ''' <remarks></remarks>
    Public Function FileAndExt(ByVal strPath As String) As String
        Dim outp As String = ""

        Try
            Dim shtPos(255) As UShort   'The recorded positions of the strBAK character(s) in strPath
            Dim shtStart As UShort      'The start position inside strPath
            Dim blnFound As Boolean     'Flag which determines if a strBAK character was found
            Dim bytItems As Byte        'The amount of strBAK characters found

            Dim shtLen As UShort        'The length of the strPath
            Dim shtFileLen As UShort    'The length of the file+ext

            bytItems = 1
            blnFound = False
            shtStart = 1

            Do
                shtPos(bytItems) = InStr(shtStart, strPath, strBAK) 'Find the next strBAK character, record its position in array
                If shtPos(bytItems) <> 0 Then
                    'If strBAK is found
                    blnFound = True
                    shtStart = shtPos(bytItems) + 1 'Set shtStart to one past the position of the found strBAK character
                    bytItems += 1                   'Increment the amount of strBAK characters found
                Else
                    'if strBAK NOT found, trigger flag to exit loop
                    blnFound = False
                End If
            Loop Until blnFound = False 'Loop until strBAK is NOT found

            shtLen = Len(strPath) 'Get the length of the filepath
            shtFileLen = shtLen - shtPos(bytItems - 1)  'Set the length of the file+ext
            outp = Mid(strPath, (shtPos(bytItems - 1)) + 1, shtFileLen) 'Extract the file+ext
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error in FileAndExt")
        End Try
        Return outp
    End Function

    ''' <summary>
    ''' Writes File.txt at Path with list of Files (rel dirs).
    ''' </summary>
    ''' <param name="Path">Directory</param>
    ''' <param name="Files">Array of filenames</param>
    ''' <remarks>Used for -i param for ffmpeg.exe</remarks>
    Private Sub WriteTxtFile(ByVal Path As String, ByRef Files() As String)
        Try
            Dim TextFile As String = Path & strBAK & "File.txt" 'The filepath to write
            'If the textfile exists, remove it for clean slate
            If My.Computer.FileSystem.FileExists(TextFile) Then My.Computer.FileSystem.DeleteFile(TextFile)

            Dim xFileData As StreamWriter           'Streamwriter object to write File.txt
            xFileData = File.CreateText(TextFile)   'Create File.txt

            Dim i As Byte = 0                       'Generic iterator
            Dim count As Byte = Files.Length - 1    'Count of files in list (0-based)
            Dim line As String = ""                 'Line to write to file

            'Iterate through the files, 0 to count
            For i = 0 To count Step 1
                line = "file " & Files(i)   'Line = "file myFilename.blah"
                xFileData.WriteLine(line)   'Write the line
            Next i
            'Close and dispose the SW
            xFileData.Close()
            xFileData.Dispose()
            xFileData = Nothing
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "File IO error in WriteTxtFile!")
        End Try
    End Sub

    ''' <summary>
    ''' Deletes files from a folder based on a DOS search spec
    ''' </summary>
    ''' <param name="Folder">Folder to search</param>
    ''' <param name="type">Search spec</param>
    ''' <remarks>
    ''' Like del cmd. For stuff like del *.pdf.
    ''' https://stackoverflow.com/questions/25429791/how-do-i-delete-all-files-of-a-particular-type-from-a-folder
    ''' </remarks>
    Private Sub DeleteFilesFromFolder(ByVal Folder As String, ByVal type As String)
        'If folder exists
        Try
            If Directory.Exists(Folder) Then
                'Iterate through all files that match spec, delete them
                For Each _file As String In Directory.GetFiles(Folder, type)
                    File.Delete(_file)
                Next _file
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "File IO error in DeleteFilesFromFolder!")
        End Try
    End Sub

    ''' <summary>
    ''' Like DeleteFIlesFromFolder, but for sequentially named files, deletes files past a limit
    ''' </summary>
    ''' <param name="Folder">Folder to search</param>
    ''' <param name="type">Search spec</param>
    ''' <param name="limit">Index limit</param>
    ''' <remarks></remarks>
    Private Sub DeleteExtraFilesFromFolder(ByVal Folder As String, ByVal type As String, ByVal limit As UShort)
        'If folder exists
        Try
            If Directory.Exists(Folder) Then
                Dim i As UShort = 1                                             'Generic counter
                For Each _file As String In Directory.GetFiles(Folder, type)
                    If i > limit Then File.Delete(_file) '                       If iterator is above limit (extra file), delete it
                    i += 1                                                      'Incremen
                Next _file
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "File IO error in DeleteExtraFilesFromFolder!")
        End Try
    End Sub

    ''' <summary>
    ''' Counts amount of files in a folder meeting a search criteria
    ''' </summary>
    ''' <param name="Folder">Folder to search</param>
    ''' <param name="type">Search spec</param>
    ''' <returns></returns>
    ''' <remarks>Similar to inputs in DeleteFilesFromFolder</remarks>
    Private Function CountFilesFromFolder(ByVal Folder As String, ByVal type As String) As UShort
        Dim cnt As UShort = 0                                               'Amount of files
        Try
            'If folder exists
            If Directory.Exists(Folder) Then
                Dim Files() As String = Directory.GetFiles(Folder, type)    'Get array of files meeting spec
                cnt = Files.Count()                                         'Get count of files meeting spec
            End If
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "File IO error in CountFilesFromFolder!")
        End Try
        Return cnt
    End Function

    ''' <summary>
    ''' Copies i_view32.ini APPDATA file pointed to from INI_Folder var within i_view32.ini (at i_view32.exe folder) to THP working directory, hacks JPG "Save Progressive" value to 0 and changes "Save Quality" setting.
    ''' This file will be used later for JPG conversion, to ensure NO Progressive JPG (fixes THPConv encoding bugs)

    ''' This is a bugfix for Bug #5.
    ''' Bug: Using a spaceless symlink to iview creates non-progressive JPG files which work with THPConv
    ''' (due to inability to find i_view32.ini file which usually has JPG "Save Progressive" option as true,
    ''' while using a real path within Program Files breaks (because it finds the INI files and enables option)
    ''' </summary>
    ''' <param name="workDir">THP working directory</param>
    ''' <returns>Sucessful INI hack?</returns>
    Private Function HackINIFile(ByVal workDir As String)
        Dim success As Boolean = False
        Const INI As String = "i_view32.ini"             'File name for Irfanview INI files
        Const INITEMP As String = "i_view32_temp.ini"    'File name for temp Irfanview INI file

        Dim xrINIData As StreamReader            'Streamreader object for reading the i_view32.ini files
        Dim xwINIData As StreamWriter            'Streamwriter object for hacked INI file
        Try
            Dim iView As String = txtiView.Text                         'irfanview exe path
            Dim iViewPath As String = Path.GetDirectoryName(iView)      'path of irfanview exe
            Dim iViewINI As String                                      '1st INI file (at exe dir)
            Dim iViewINI2 As String                                     '2nd options INI file (at %Appdata%/whatever)
            Dim iViewINI2Temp As String                                 'Same as iViewINI2, but temporary INI that will be hacked

            iViewPath &= (strBAK & INI)                                 'Get the 1st INI file (exe folder\INI const)
            Dim strEntry As String                                      'Line from INI file
            Dim blnFound As Boolean = False                             'String match found?
            Dim chrInd As Integer                                       'Index of a char in line

            xrINIData = File.OpenText(iViewPath)                         'Open the INI file

            'Read each line, until find "[Others]" marker
            blnFound = False
            While xrINIData.EndOfStream() <> True
                strEntry = xrINIData.ReadLine()                    'Read a line from the file

                'If marker found, flag as found and exit loop
                If strEntry.Contains("[Others]") Then
                    blnFound = True
                    Exit While
                End If
            End While

            'If marker was not found, throw custom error
            If blnFound = False Then Throw New System.Exception("Failed to find '[Others]' marker in i_view32.ini file within Irfanview exe folder")

            'Read next line from the file
            strEntry = xrINIData.ReadLine()
            'If next line is not INI_Folder variable, then throw custom error
            If strEntry.Contains("INI_Folder") = False Then Throw New System.Exception("Failed to find 'INI_Folder' variable in i_view32.ini file within Irfanview exe folder")

            chrInd = strEntry.IndexOf("=")                              'Find index of '=' char in variable line
            chrInd += 2                                                 'Increment by 2. !@ This may be wrong?
            iViewINI = Mid(strEntry, chrInd)                            'Get substring of everything after = sign
            iViewINI = LTrim(iViewINI)                                  'Left trim it
            iViewINI &= (strBAK & INI)                                  'Add "\INI" to EOP
            iViewINI = Environment.ExpandEnvironmentVariables(iViewINI) 'Expand any environ vars within (defaul INI usually has %APPDATA% envvar)

            'Close INI file, copy over INI file, then load it for reading/writing to new temp file
            xrINIData.Close()
            xrINIData.Dispose()
            xrINIData = Nothing
            iViewINI2 = workDir & strBAK & INI
            iViewINI2Temp = workDir & strBAK & INITEMP
            My.Computer.FileSystem.CopyFile(iViewINI, iViewINI2, True)
            xrINIData = File.OpenText(iViewINI2)                                        'Open the INI2 file
            xwINIData = My.Computer.FileSystem.OpenTextFileWriter(iViewINI2Temp, False) 'Open a new INI2Temp file for writing

            'Read each line, write each to other file, read until find "[JPEG]" marker
            blnFound = False
            While xrINIData.EndOfStream() <> True
                strEntry = xrINIData.ReadLine()                    'Read a line from the file
                xwINIData.WriteLine(strEntry)

                'If marker found, flag as found and exit loop
                If strEntry.Contains("[JPEG]") Then
                    blnFound = True
                    Exit While
                End If
            End While

            'If marker was not found, throw custom error
            If blnFound = False Then Throw New System.Exception("Failed to find '[JPEG]' marker in " & strQUOT & iViewINI2 & strQUOT & " file.")

            'Keep reading lines and writing to other file until EOF
            'If found "Save Progressive=BIT" line, change BIT to 0 (this is the INI hack)
            'If Found "Save Quality" line, replace value with JPG quality (the other hack)
            blnFound = False
            While xrINIData.EndOfStream() <> True
                strEntry = xrINIData.ReadLine()                    'Read a line from the file

                'If Save Progressive var found, replace 1 to 0 bit
                If strEntry.Contains("Save Progressive") Then
                    blnFound = (blnFound Or True)
                    strEntry = strEntry.Replace("1", "0")
                End If

                'If Save Quality var found, replace with new quality
                If strEntry.Contains("Save Quality") Then
                    blnFound = (blnFound Or True)
                    strEntry = "Save Quality=" & nudTE_jpgq.Value.ToString()
                End If

                'Write line
                xwINIData.WriteLine(strEntry)
            End While

            'If markers were not found, throw error
            If blnFound = False Then Throw New System.Exception("Failed to find 'Save Progressive' or 'Save Quality' settings under '[JPEG]' marker in " & strQUOT & iViewINI2 & strQUOT & " file. Irfanview INI hack was unsuccessful, and thus ripped JPG files used for THP Encoding may be created as wrong progressive JPG types or wrong JPG quality applied. These errors shall cause THP encoding to fail!")

            'Close all files, delete iViewINI2, rename iViewINI2TEMP to iViewINI2, show success
            xrINIData.Close()
            xrINIData.Dispose()
            xrINIData = Nothing
            xwINIData.Close()
            xwINIData.Dispose()
            xwINIData = Nothing
            My.Computer.FileSystem.DeleteFile(iViewINI2)
            My.Computer.FileSystem.RenameFile(iViewINI2Temp, Path.GetFileName(iViewINI2))
            MsgBox("Disabled Progressive JPG for conversions!", MsgBoxStyle.Information, "Irfanview settings INI Hack successful!")
            success = True
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical, "Error finding, copying, and/or hacking INI Irfanview INI file!")
            success = False
        End Try

        'Close any lingering Streams if not null
        If IsNothing(xrINIData) = False Then
            xrINIData.Close()
            xrINIData.Dispose()
            xrINIData = Nothing
        End If

        If IsNothing(xwINIData) = False Then
            xwINIData.Close()
            xwINIData.Dispose()
            xwINIData = Nothing
        End If

        Return success
    End Function

    '================
    'Cast helper funcs

    Private Function BitToBool(ByVal inp As Byte) As Boolean
        Dim outp As Boolean = False
        If inp = 1 Then outp = True
        Return outp
    End Function

    'Converts String with "True" or "False" into appropriate boolean value
    'In = Boolean String
    'Out = Boolean value
    Private Function BoolStrToBool(ByVal inp As String) As Boolean
        Dim outp As Boolean = False      'Output value
        If inp = "True" Then outp = True 'If "True" then true
        Return outp
    End Function

    ''' <summary>
    ''' Changes a boolean checkbox's string based on state
    ''' </summary>
    ''' <param name="T">True string</param>
    ''' <param name="F">False string</param>
    ''' <param name="box">Checkbox</param>
    ''' <remarks></remarks>
    Private Sub ChkString(ByVal T As String, ByVal F As String, ByRef box As System.Windows.Forms.CheckBox)
        Dim v As String
        If box.Checked = True Then v = T Else v = F
        box.Text = v
    End Sub

    ''' <summary>
    ''' Try parsing a string as byte; if fail, throw error
    ''' </summary>
    ''' <param name="inp">String</param>
    ''' <returns>Byte</returns>
    ''' <remarks></remarks>
    Private Function TryParseErr_Byte(ByVal inp As String) As Byte
        Dim outp As Byte = 0
        Dim result As Boolean = Byte.TryParse(inp, outp)
        If result = False Then
            Throw New System.Exception("Error parsing string into Byte")
        End If
        Return outp
    End Function
    ''' <summary>
    ''' Try parsing a string as UShort; if fail, throw error
    ''' </summary>
    ''' <param name="inp">String</param>
    ''' <returns>Byte</returns>
    ''' <remarks></remarks>
    Private Function TryParseErr_UShort(ByVal inp As String) As UShort
        Dim outp As UShort = 0
        Dim result As Boolean = UShort.TryParse(inp, outp)
        If result = False Then
            Throw New System.Exception("Error parsing string into UShort")
        End If
        Return outp
    End Function
    ''' <summary>
    ''' Try parsing a string as Single; if fail, throw error
    ''' </summary>
    ''' <param name="inp">String</param>
    ''' <returns>Byte</returns>
    ''' <remarks></remarks>
    Private Function TryParseErr_Single(ByVal inp As String) As Single
        Dim outp As Single = 0
        Dim result As Boolean = Single.TryParse(inp, outp)
        If result = False Then
            Throw New System.Exception("Error parsing string into Single")
        End If
        Return outp
    End Function
End Class