using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using ReceptionKiosk.Helpers;
using ReceptionKiosk.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace ReceptionKiosk.ViewModels
{
    public class AddFaceViewModel : Observable
    {
        /// <summary>
        /// Resource loader for localized strings
        /// </summary>
        ResourceLoader loader = new Windows.ApplicationModel.Resources.ResourceLoader();

        /// <summary>
        /// FaceServiceClient Instanz
        /// </summary>
        private FaceServiceClient FaceService { get; set; }

        #region Commands

        public ICommand AddPersonCommand { get; set; }

        public ICommand BrowsePictureCommand { get; set; }

        public ICommand RemoveImageCommand { get; set; }

        #endregion //Commands

        #region Properties

        private bool _isLoading;
        public bool IsLoading
        {
            get { return _isLoading; }
            set { Set(ref _isLoading, value); }
        }

        private string _newFaceName = String.Empty;
        public string NewFaceName
        {
            get { return _newFaceName; }
            set { Set(ref _newFaceName, value); }
        }

        private PersonGroup _selectedPersonGroup;
        public PersonGroup SelectedPersonGroup
        {
            get { return _selectedPersonGroup; }
            set { Set(ref _selectedPersonGroup, value); }
        }

        #endregion

        /// <summary>
        /// Liste aller verf�gbaren Gruppen
        /// </summary>
        public ObservableCollection<PersonGroup> PersonGroups { get; private set; }

        /// <summary>
        /// Liste aller Bilder
        /// </summary>
        public ObservableCollection<BitmapWrapper> Pictures { get; private set; }

        public AddFaceViewModel()
        {
            PersonGroups = new ObservableCollection<PersonGroup>();
            Pictures = new ObservableCollection<BitmapWrapper>();

            AddPersonCommand = new RelayCommand(async () => await ExecuteAddPersonCommand());
            BrowsePictureCommand = new RelayCommand(async () => await ExecuteBrowsePictureCommandAsync());
            RemoveImageCommand = new RelayCommand(async () => await ExecuteRemoveImageCommand());
        }

        /// <summary>
        /// Removes images from list
        /// </summary>
        /// <returns></returns>
        private Task ExecuteRemoveImageCommand()
        {
            throw new NotImplementedException();
        }


        #region BrowsePictureCommand

        private async Task ExecuteBrowsePictureCommandAsync()
        {
            try
            {
                FileOpenPicker fileOpenPicker = new FileOpenPicker() { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
                fileOpenPicker.FileTypeFilter.Add(".jpg");
                fileOpenPicker.FileTypeFilter.Add(".jpeg");
                fileOpenPicker.FileTypeFilter.Add(".png");
                fileOpenPicker.FileTypeFilter.Add(".bmp");
                IReadOnlyList<StorageFile> selectedFiles = await fileOpenPicker.PickMultipleFilesAsync();

                if (selectedFiles != null)
                {
                    foreach (var item in selectedFiles)
                    {
                        SoftwareBitmap softwareBitmap;

                        using (IRandomAccessStream stream = await item.OpenAsync(FileAccessMode.Read))
                        {
                            // Create the decoder from the stream
                            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                            // Get the SoftwareBitmap representation of the file
                            softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                            softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                            var softwareBitmapSource = new SoftwareBitmapSource();
                            await softwareBitmapSource.SetBitmapAsync(softwareBitmap);

                            Pictures.Add(new BitmapWrapper(softwareBitmap, softwareBitmapSource));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message).ShowAsync();
            }
        }

        #endregion //BrowsePictureCommand

        #region Eventhandler

        public void SelectionChanged(object sender, RoutedEventArgs e)
        {
            var cb = sender as ComboBox;
            SelectedPersonGroup = cb.SelectedItem as PersonGroup;
        }

        #endregion

        private async Task ExecuteAddPersonCommand()
        {
            if (NewFaceName != string.Empty && Pictures.Count > 0 && SelectedPersonGroup != null)
            {
                IsLoading = true;
                try
                {
                    List<AddPersistedFaceResult> faces = new List<AddPersistedFaceResult>();

                    var result = await FaceService.CreatePersonAsync(SelectedPersonGroup.PersonGroupId, NewFaceName);

                    foreach (var picture in Pictures)
                    {
                        var currentPicture = picture.Bitmap;

                        IRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();

                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, randomAccessStream);

                        encoder.SetSoftwareBitmap(currentPicture);

                        await encoder.FlushAsync();

                        var stream = randomAccessStream.AsStreamForRead();

                        faces.Add(await FaceService.AddPersonFaceAsync(SelectedPersonGroup.PersonGroupId, result.PersonId, stream));
                    }

                    await new MessageDialog($"Successfully added {faces.Count} faces for person {NewFaceName} ({result.PersonId}).").ShowAsync();

                    //Reset the form
                    Pictures.Clear();
                    NewFaceName = "";
                }
                catch (FaceAPIException e)
                {
                    await new MessageDialog(e.ErrorMessage).ShowAsync();
                    //await new MessageDialog(loader.GetString("AddFace_CompleteInformation")).ShowAsync();
                }
                finally
                {
                    IsLoading = false;
                }
            }
            else
            {
                await new MessageDialog(loader.GetString("AddFace_CompleteInformation")).ShowAsync();
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;

                if (FaceService == null)
                    FaceService = await FaceServiceHelper.CreateNewFaceServiceAsync();

                var personGroupResult = await FaceService.ListPersonGroupsAsync();
                personGroupResult.OrderBy(pg => pg.Name);
                personGroupResult.ForEach(pg => PersonGroups.Add(pg));

                IsLoading = false;
            }
            catch (FaceAPIException ex)//Handle API-Exception
            {
                await MessageDialogHelper.MessageDialogAsync(ex.ErrorMessage);
            }
            catch (Exception ex)
            {
                await MessageDialogHelper.MessageDialogAsync(ex.Message);
            }
        }
    }
}
