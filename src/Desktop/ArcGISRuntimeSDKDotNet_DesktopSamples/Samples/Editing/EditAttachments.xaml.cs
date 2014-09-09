﻿using System.Threading.Tasks;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Layers;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

namespace ArcGISRuntimeSDKDotNet_DesktopSamples.Samples
{
    /// <summary>
    /// Demonstrates how attachments can be queried and modified from a ServiceFeatureTable and how this type of edit is pushed to the server or canceled.
    /// </summary>
    /// <title>Edit Attachments</title>
    /// <category>Editing</category>
    public partial class EditAttachments : UserControl
    {
        public EditAttachments()
        {
            InitializeComponent();
        }
        
        private async Task QueryAttachmentsAsync(ServiceFeatureTable table, long featureID)
        {
            if (table == null)
                return;
            // By default, the attachmentResults is a union of local and server query for attachments.
            var queryAttachmentResult = await table.QueryAttachmentsAsync(featureID);
            AttachmentList.ItemsSource = queryAttachmentResult != null ? queryAttachmentResult.Infos : null;
        }

        private async void MyMapView_MapViewTapped(object sender, MapViewInputEventArgs e)
        {
            if (MyMapView.Map == null || MyMapView.Map.Layers == null)
                return;
            var layer = MyMapView.Map.Layers["IncidentsLayer"] as FeatureLayer;
            if (layer == null)
                return;
            var table = layer.FeatureTable as ServiceFeatureTable;
            if (table == null)
                return;
            // Updates layer selection and attachment list.
            layer.ClearSelection();
            AttachmentList.ItemsSource = null;
            string message = null;
            try
            {
                // Selects feature based on hit-test attachmentResults 
                // and performs query attachments on the first selected feature.
                var featureIDs = await layer.HitTestAsync(MyMapView, e.Position);
                if (featureIDs != null)
                {
                    layer.SelectFeatures(featureIDs);
                    await QueryAttachmentsAsync(table, featureIDs.FirstOrDefault());
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }
        
        private static bool GetFile(out Stream stream, out string name)
        {
            stream = Stream.Null;
            name = null;
            var dialog = new OpenFileDialog();
            dialog.InitialDirectory = Path.Combine(Environment.CurrentDirectory, "images");
            dialog.Multiselect = false;
            dialog.Filter = "All Files (*.*)|*.*|Image Files|*.tif;*.jpg;*.gif;*.png;*.bmp|Text Files (.txt)|*.txt";
            var dialogResult = dialog.ShowDialog();
            if (!dialogResult.HasValue || !dialogResult.Value)
                return false;
            stream = dialog.OpenFile();
            name = Path.GetFileName(dialog.FileName);
            return true;
        }

        private enum EditType
        {
            Add,
            Update,
            Delete
        }

        private async Task UpdateAttachmentListAsync(AttachmentResult attachmentResult, EditType editType)
        {
            if (attachmentResult == null)
                return;
            if (MyMapView.Map == null || MyMapView.Map.Layers == null)
                return;
            var layer = MyMapView.Map.Layers["IncidentsLayer"] as FeatureLayer;
            if (layer == null || layer.SelectedFeatureIDs == null || !layer.SelectedFeatureIDs.Any())
                return;
            var table = layer.FeatureTable as ServiceFeatureTable;
            if (table == null)
                return;
            string message = null;
            try
            {
                if (attachmentResult != null)
                {
                    if (attachmentResult.Error != null)
                        message = attachmentResult.Error.Message;
                    else
                    {
                        message = string.Format("{0} attachment [{1}] {2} feature [{3}]",
                            editType == EditType.Add ? "Add" : (editType == EditType.Update ? "Update" : "Delete"),
                            attachmentResult.ObjectID,
                            editType == EditType.Add ? "to" : "from",
                            attachmentResult.ParentID);
                    }
                    // Performs another query on attachments.
                    await QueryAttachmentsAsync(table, attachmentResult.ParentID);
                    SaveButton.IsEnabled = table.HasEdits;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (MyMapView.Map == null || MyMapView.Map.Layers == null)
                return;
            var layer = MyMapView.Map.Layers["IncidentsLayer"] as FeatureLayer;
            if (layer == null || layer.SelectedFeatureIDs == null || !layer.SelectedFeatureIDs.Any())
                return;
            var table = layer.FeatureTable as ServiceFeatureTable;
            if (table == null)
                return;
            var featureID = layer.SelectedFeatureIDs.FirstOrDefault();
            var stream = Stream.Null;
            string name = null;
            string message = null;
            try
            {
                if (GetFile(out stream, out name))
                {
                    // Adds attachment to the specified feature.
                    var attachmentResult = await table.AddAttachmentAsync(featureID, stream, name);
                    await UpdateAttachmentListAsync(attachmentResult, EditType.Add);
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (MyMapView.Map == null || MyMapView.Map.Layers == null)
                return;
            var layer = MyMapView.Map.Layers["IncidentsLayer"] as FeatureLayer;
            if (layer == null || layer.SelectedFeatureIDs == null || !layer.SelectedFeatureIDs.Any())
                return;
            var table = layer.FeatureTable as ServiceFeatureTable;
            if (table == null)
                return;
            var info = (sender as Button).DataContext as AttachmentInfoItem;
            if (info == null)
                return;
            var featureID = layer.SelectedFeatureIDs.FirstOrDefault();
            var stream = Stream.Null;
            string name = null;
            string message = null;
            try
            {
                if (GetFile(out stream, out name))
                {
                    // Updates the specified attachment.
                    var attachmentResult = await table.UpdateAttachmentAsync(featureID, info.ID, stream, name);
                    await UpdateAttachmentListAsync(attachmentResult, EditType.Update);
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MyMapView.Map == null || MyMapView.Map.Layers == null)
                return;
            var layer = MyMapView.Map.Layers["IncidentsLayer"] as FeatureLayer;
            if (layer == null || layer.SelectedFeatureIDs == null || !layer.SelectedFeatureIDs.Any())
                return;
            var table = layer.FeatureTable as ServiceFeatureTable;
            if (table == null)
                return;
            var info = (sender as Button).DataContext as AttachmentInfoItem;
            if (info == null)
                return;
            var featureID = layer.SelectedFeatureIDs.FirstOrDefault();
            string message = null;
            try
            {
                // Deletes the specified attachment.
                var deleteAttachmentResult = await table.DeleteAttachmentsAsync(featureID, new long[] { info.ID });
                var attachmentResult = deleteAttachmentResult != null && deleteAttachmentResult.Results != null ?
                    deleteAttachmentResult.Results.FirstOrDefault() : null;
                await UpdateAttachmentListAsync(attachmentResult, EditType.Delete);
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as Button).DataContext as AttachmentInfoItem;
            if (info == null)
                return;
            Image image = null;
            string message = null;
            try
            {
                // OnlineAttachmentInfoItem is returned if attachment was not marked for edit.
                if (info is OnlineAttachmentInfoItem)
                {
                    var onlineItem = (OnlineAttachmentInfoItem)info;
                    // Uri property is only available on OnlineAttachmentInfoItem.
                    image = new Image() { Source = new BitmapImage(onlineItem.Uri) };
                }
                else
                {
                    // Otherwise, retrieve the local attachment stream data.
                    var data = await info.GetDataAsync();
                    if (data != Stream.Null)
                    {
                        var source = new BitmapImage();
                        source.BeginInit();
                        source.StreamSource = data;
                        source.EndInit();
                        image = new Image() { Source = source };
                    }
                }
                // Displays the attachment as an image onto a new dialog window.
                if (image != null)
                {
                    var window = new Window() { Content = image, Title = info.Name };
                    window.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }

        private static string GetResultMessage(IEnumerable<AttachmentResult> attachmentResults, EditType editType)
        {
            var sb = new StringBuilder();
            var operation = editType == EditType.Add ? "adds" :
                (editType == EditType.Update ? "updates" : "deletes");
            if (attachmentResults.Any(r => r.Error != null))
            {
                sb.AppendLine(string.Format("Failed {0} : [{1}]", operation, string.Join(", ", from r in attachmentResults
                                                                                               where r.Error != null
                                                                                               select string.Format("{0} : {1}", r.ObjectID, r.Error != null ? r.Error.Message : string.Empty))));
            }
            if (attachmentResults.Any(r => r.Error == null))
            {
                sb.AppendLine(string.Format("Successful {0} : [{1}]", operation, string.Join(", ", from r in attachmentResults
                                                                                                   where r.Error == null
                                                                                                   select r.ObjectID)));
            }
            return sb.ToString();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (MyMapView.Map == null || MyMapView.Map.Layers == null)
                return;
            var layer = MyMapView.Map.Layers["IncidentsLayer"] as FeatureLayer;
            if (layer == null || layer.SelectedFeatureIDs == null || !layer.SelectedFeatureIDs.Any())
                return;
            var table = layer.FeatureTable as ServiceFeatureTable;
            if (table == null || !table.HasEdits)
                return;
            string message = null;
            try
            {
                // Submits the attachment edits to server.
                var saveResult = await table.ApplyAttachmentEditsAsync();
                if (saveResult != null)
                {
                    var sb = new StringBuilder();
                    var editMessage = GetResultMessage(saveResult.AddResults, EditType.Add);
                    if (!string.IsNullOrWhiteSpace(editMessage))
                        sb.AppendLine(editMessage);
                    editMessage = GetResultMessage(saveResult.UpdateResults, EditType.Update);
                    if (!string.IsNullOrWhiteSpace(editMessage))
                        sb.AppendLine(editMessage);
                    if (saveResult.DeleteResults != null)
                    {
                        foreach (var deleteResult in saveResult.DeleteResults)
                        {
                            editMessage = GetResultMessage(deleteResult.Results, EditType.Delete);
                            if (!string.IsNullOrWhiteSpace(editMessage))
                                sb.AppendLine(editMessage);
                        }
                    }
                    message = sb.ToString();
                }
                SaveButton.IsEnabled = table.HasEdits;
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (MyMapView.Map == null || MyMapView.Map.Layers == null)
                return;
            var layer = MyMapView.Map.Layers["IncidentsLayer"] as FeatureLayer;
            if (layer == null || layer.SelectedFeatureIDs == null || !layer.SelectedFeatureIDs.Any())
                return;
            var table = layer.FeatureTable as ServiceFeatureTable;
            if (table == null || !table.HasEdits)
                return;
            string message = null;
            try
            {
                // Cancels the local edits by refreshing features with preserveEdits=false.
                table.RefreshFeatures(false);
                var featureID = layer.SelectedFeatureIDs.FirstOrDefault();
                await QueryAttachmentsAsync(table, featureID);
                SaveButton.IsEnabled = table.HasEdits;
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            if (!string.IsNullOrWhiteSpace(message))
                MessageBox.Show(message);
        }
    }
}
