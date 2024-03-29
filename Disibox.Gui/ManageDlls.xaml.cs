﻿//
// Copyright (c) 2011, University of Genoa
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the University of Genoa nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL UNIVERSITY OF GENOA BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Disibox.Data.Client;
using Disibox.Data.Client.Exceptions;
using Disibox.Data.Exceptions;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace Disibox.Gui
{
    /// <summary>
    /// Interaction logic for ManageDlls.xaml
    /// </summary>
    public partial class ManageDlls : Window {
        private readonly ClientDataSource _ds;
        public ManageDlls(ClientDataSource ds)
        {
            InitializeComponent();
            _ds = ds;
            RefreshDlls();
            ShowDialog();
        }

        private void RefreshDlls() {
            IList<string> dlls = null;

            const string titleMessageBox = "Refreshing dlls list";
            const string messageMessageBox = "Error while refreshing the dlls list: ";

            try {
                dlls = _ds.GetProcessingDllNames();
            } catch (UserNotAdminException) {
                MessageBox.Show(messageMessageBox + "you are not admin.", titleMessageBox);
            } catch (UserNotLoggedInException) {
                MessageBox.Show(messageMessageBox + "you are not logged in.", titleMessageBox);
            }

            if (dlls == null)
                return;

            listView_Dlls.Items.Clear();

            foreach (var dll in dlls)
                listView_Dlls.Items.Add(dll);
        }

        private void buttonRefreshDlls_Click(object sender, RoutedEventArgs e) {
           RefreshDlls();
        }

        private void buttonDeleteDll_Click(object sender, RoutedEventArgs e) {
            var selectedItem = (string) listView_Dlls.SelectedItem;

            if (selectedItem == null)
                return;

            const string titleMessageBox = "Deleting dll";
            const string messageMessageBox = "Error while deleting the dll from the cloud: ";
            try {
                _ds.DeleteProcessingDll(selectedItem);
            } catch (UserNotLoggedInException) {
                MessageBox.Show(messageMessageBox + "you are not logged in!", titleMessageBox);
                return;
            } catch (UserNotAdminException) {
                MessageBox.Show(messageMessageBox + "you are not admin.", titleMessageBox);
            } catch (Exception) {
                MessageBox.Show(messageMessageBox + "unknown error!", titleMessageBox);
                return;
            }

            //MessageBox.Show(ok ? "The file was deleted successfully" : "Error deleting the file", titleMessageBox);

            RefreshDlls();

        }

        private void buttonDownloadDll_Click(object sender, RoutedEventArgs e) {
            var selectedItem = (string) listView_Dlls.SelectedItem;
            var saveDialog = new SaveFileDialog();
            FileStream destinationFile;
            Stream fileToDownload = null;

            if (selectedItem == null || saveDialog.ShowDialog() != true || !saveDialog.CheckPathExists) return;

            const string titleMessageBox = "Downloading dll";
            const string messageMessageBox = "Error while downloading the dll from the cloud: ";

            try {
                fileToDownload = _ds.GetProcessingDll(selectedItem);
            } catch (UserNotLoggedInException) {
                MessageBox.Show(messageMessageBox + "you are not logged in!", titleMessageBox);
                return;
            } catch (UserNotAdminException) {
                MessageBox.Show(messageMessageBox + "you are not admin.", titleMessageBox);
            } catch (Exception) {
                MessageBox.Show(messageMessageBox + "unknown error!", titleMessageBox);
                return;
            }

            if (fileToDownload == null) {
                MessageBox.Show(messageMessageBox + "unknown error!", titleMessageBox);
                return;
            }

            //catch exception if any
            try {
                destinationFile = File.Create(saveDialog.FileName);
            } catch (Exception) {
                MessageBox.Show(messageMessageBox + "creating destination file!", titleMessageBox);
                return;
            }

            try {
                fileToDownload.CopyTo(destinationFile);
            } catch (Exception ex) {
                MessageBox.Show(messageMessageBox + ex, titleMessageBox);
                destinationFile.Close();
                return;
            }

            destinationFile.Close();
            MessageBox.Show("File successfuly downloaded to: " + saveDialog.FileName, titleMessageBox);
        
        }

        private void buttonAddDll_Click(object sender, RoutedEventArgs e) {
            var ofd = new OpenFileDialog();
            var result = ofd.ShowDialog();

            if (result != true) return;

            var titleMessageBox = "Uploading Dll";
            const string messageMessageBox = "Error while uploading the dll to the cloud: ";

            var fileName = Path.GetFileName(ofd.FileName);
            FileStream fileStream;

            try {
                fileStream = new FileStream(ofd.FileName, FileMode.Open);
            } catch (Exception ex) {
                MessageBox.Show("The dll file to upload cannot be opened: " + ex, "Uploading a dll");
                return;
            }

            var questionResult = MessageBoxResult.Cancel;
            try {
                _ds.AddProcessingDll(fileName, fileStream);
            } catch (FileExistingException) {
                questionResult = MessageBox.Show("This dll already exists on the cloud, " +
                                                  "do you want to overwrite it?",
                                                  "Uploading a file", MessageBoxButton.YesNo);
            } catch (InvalidFileNameException) {
                MessageBox.Show(messageMessageBox + "filename is invalid!", titleMessageBox);
                fileStream.Close();
                return;
            } catch (InvalidContentTypeException) {
                MessageBox.Show(messageMessageBox + "content type is invalid!", titleMessageBox);
                fileStream.Close();
                return;
            } catch (ArgumentNullException) {
                MessageBox.Show(messageMessageBox + "argument is null!", titleMessageBox);
                fileStream.Close();
                return;
            } catch (UserNotLoggedInException) {
                MessageBox.Show(messageMessageBox + "you are not logged in!", titleMessageBox);
                fileStream.Close();
                return;
            } catch (UserNotAdminException) {
                MessageBox.Show(messageMessageBox + "you are not admin!", titleMessageBox);
                fileStream.Close();
                return;
            } catch (Exception) {
                MessageBox.Show(messageMessageBox + "unknown error.", titleMessageBox);
                fileStream.Close();
                return;
            }


            switch (questionResult) {
                case MessageBoxResult.Cancel:
                    MessageBox.Show("The dll has been uploaded successfully!", titleMessageBox);
                    RefreshDlls();
                    break;

                case MessageBoxResult.No:
                    MessageBox.Show("The dll on the cloud has been NOT overwritten by the local file!",
                                    titleMessageBox);
                    break;

                case MessageBoxResult.Yes:
                    titleMessageBox += " (Overwritting)";
                    try {
                        _ds.AddProcessingDll(fileName, fileStream, true);
                    } catch (InvalidFileNameException) {
                        MessageBox.Show(messageMessageBox + "filename is invalid!", titleMessageBox);
                        fileStream.Close();
                        return;
                    } catch (InvalidContentTypeException) {
                        MessageBox.Show(messageMessageBox + "content type is invalid!", titleMessageBox);
                        fileStream.Close();
                        return;
                    } catch (ArgumentNullException) {
                        MessageBox.Show(messageMessageBox + "argument is null!", titleMessageBox);
                        fileStream.Close();
                        return;
                    } catch (UserNotLoggedInException) {
                        MessageBox.Show(messageMessageBox + "you are not logged in!", titleMessageBox);
                        fileStream.Close();
                        return;
                    } catch (UserNotAdminException) {
                        MessageBox.Show(messageMessageBox + "you are not admin!", titleMessageBox);
                        fileStream.Close();
                        return;
                    } catch (Exception) {
                        MessageBox.Show(messageMessageBox + "unknown error.", titleMessageBox);
                        fileStream.Close();
                        return;
                    }

                    MessageBox.Show("The dll has been uploaded successfully!", titleMessageBox);
                    RefreshDlls();
                    break;
            }

            fileStream.Close();
        }


    }
}
