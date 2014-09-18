using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Devices;
using System.Windows.Input;
using ZXing;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Maps.Controls;
using System.Device.Location;
using Windows.Devices.Geolocation;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Threading.Tasks;
using Windows.Phone.Speech.Synthesis;

namespace RideNow3
{
    public partial class MainPage : PhoneApplicationPage
    {

        private PhotoCamera _phoneCamera;
        private IBarcodeReader _barcodeReader;
        private DispatcherTimer _scanTimer;
        private WriteableBitmap _previewBuffer;

        private Map MyMap;


        // Constructor
        public MainPage()
        {
            InitializeComponent();

            MyMap = new Map();
            //MyMap.Center = new GeoCoordinate(47.6097, -122.3331);
            //MyMap.Center = new GeoCoordinate(2.9237232, 101.6462635);
            //MyMap.ZoomLevel = 15;
            ContentPanel.Children.Add(MyMap);

            ShowMyLocationOnTheMap();

            // Set the data context of the listbox control to the sample data
            DataContext = App.ViewModel;
        }

        // Load data for the ViewModel Items
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }

            // Initialize the camera object
            _phoneCamera = new PhotoCamera();
            _phoneCamera.Initialized += cam_Initialized;
            _phoneCamera.AutoFocusCompleted += _phoneCamera_AutoFocusCompleted;

            CameraButtons.ShutterKeyHalfPressed += CameraButtons_ShutterKeyHalfPressed;

            //Display the camera feed in the UI
            viewfinderBrush.SetSource(_phoneCamera);


            // This timer will be used to scan the camera buffer every 250ms and scan for any barcodes
            _scanTimer = new DispatcherTimer();
            _scanTimer.Interval = TimeSpan.FromMilliseconds(250);
            _scanTimer.Tick += (o, arg) => ScanForBarcode();

            viewfinderCanvas.Tap += new EventHandler<GestureEventArgs>(focus_Tapped);

            base.OnNavigatedTo(e);
        }




        void _phoneCamera_AutoFocusCompleted(object sender, CameraOperationCompletedEventArgs e)
        {
            Deployment.Current.Dispatcher.BeginInvoke(delegate()
            {
                focusBrackets.Visibility = Visibility.Collapsed;
            });
        }

        void focus_Tapped(object sender, GestureEventArgs e)
        {
            if (_phoneCamera != null)
            {
                if (_phoneCamera.IsFocusAtPointSupported == true)
                {
                    // Determine the location of the tap.
                    Point tapLocation = e.GetPosition(viewfinderCanvas);

                    // Position the focus brackets with the estimated offsets.
                    focusBrackets.SetValue(Canvas.LeftProperty, tapLocation.X - 30);
                    focusBrackets.SetValue(Canvas.TopProperty, tapLocation.Y - 28);

                    // Determine the focus point.
                    double focusXPercentage = tapLocation.X / viewfinderCanvas.ActualWidth;
                    double focusYPercentage = tapLocation.Y / viewfinderCanvas.ActualHeight;

                    // Show the focus brackets and focus at point.
                    focusBrackets.Visibility = Visibility.Visible;
                    _phoneCamera.FocusAtPoint(focusXPercentage, focusYPercentage);
                }
            }
        }

        void CameraButtons_ShutterKeyHalfPressed(object sender, EventArgs e)
        {
            _phoneCamera.Focus();
        }

        protected override void OnNavigatingFrom(System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            //we're navigating away from this page, we won't be scanning any barcodes
            _scanTimer.Stop();

            if (_phoneCamera != null)
            {
                // Cleanup
                _phoneCamera.Dispose();
                _phoneCamera.Initialized -= cam_Initialized;
                CameraButtons.ShutterKeyHalfPressed -= CameraButtons_ShutterKeyHalfPressed;
            }
        }

        void cam_Initialized(object sender, Microsoft.Devices.CameraOperationCompletedEventArgs e)
        {
            if (e.Succeeded)
            {
                this.Dispatcher.BeginInvoke(delegate()
                {
                    _phoneCamera.FlashMode = FlashMode.Off;
                    _previewBuffer = new WriteableBitmap((int)_phoneCamera.PreviewResolution.Width, (int)_phoneCamera.PreviewResolution.Height);

                    _barcodeReader = new BarcodeReader();

                    // By default, BarcodeReader will scan every supported barcode type
                    // If we want to limit the type of barcodes our app can read, 
                    // we can do it by adding each format to this list object

                    //var supportedBarcodeFormats = new List<BarcodeFormat>();
                    //supportedBarcodeFormats.Add(BarcodeFormat.QR_CODE);
                    //supportedBarcodeFormats.Add(BarcodeFormat.DATA_MATRIX);
                    //_bcReader.PossibleFormats = supportedBarcodeFormats;

                    _barcodeReader.Options.TryHarder = true;

                    _barcodeReader.ResultFound += _bcReader_ResultFound;
                    _scanTimer.Start();
                });
            }
            else
            {
                Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show("Unable to initialize the camera");
                });
            }
        }

        void _bcReader_ResultFound(Result obj)
        {

            //await TextToSpeech(obj.Text);

            //_scanTimer.Stop();

            // If a new barcode is found, vibrate the device and display the barcode details in the UI
            if (!obj.Text.Equals(tbBarcodeData.Text))
            {
                VibrateController.Default.Start(TimeSpan.FromMilliseconds(100));
                tbBarcodeType.Text = obj.BarcodeFormat.ToString();
                tbBarcodeData.Text = obj.Text;

                MessageBox.Show("Successful check in.\nThanks for using Ride Now.  Enjoy your journey.");

            }

            //if (scanHeader.Header == null) scanHeader.Header = "";

            //if (!obj.Text.Equals(scanHeader.Header.ToString()))
            //{
            //    VibrateController.Default.Start(TimeSpan.FromMilliseconds(100));
            //    scanHeader.Header = obj.BarcodeFormat.ToString();
            //    scanHeader.Header = obj.Text;
            //}
        }

        private void ScanForBarcode()
        {
            //grab a camera snapshot
            _phoneCamera.GetPreviewBufferArgb32(_previewBuffer.Pixels);
            _previewBuffer.Invalidate();

            //scan the captured snapshot for barcodes
            //if a barcode is found, the ResultFound event will fire
            _barcodeReader.Decode(_previewBuffer);

        }



        private async void ShowMyLocationOnTheMap()
        {

            // Get my current location.
            Geolocator myGeolocator = new Geolocator();
            Geoposition myGeoposition = await myGeolocator.GetGeopositionAsync();
            Geocoordinate myGeocoordinate = myGeoposition.Coordinate;
            GeoCoordinate myGeoCoordinate =
                CoordinateConverter.ConvertGeocoordinate(myGeocoordinate);

            // Make my current location the center of the Map.
            MyMap.Center = myGeoCoordinate;
            MyMap.ZoomLevel = 13;

            // Create a small circle to mark the current location.
            Ellipse myCircle = new Ellipse();
            myCircle.Fill = new SolidColorBrush(Colors.Red);
            myCircle.Height = 20;
            myCircle.Width = 20;
            myCircle.Opacity = 50;

            // Create a MapOverlay to contain the circle.
            MapOverlay myLocationOverlay = new MapOverlay();
            myLocationOverlay.Content = myCircle;
            myLocationOverlay.PositionOrigin = new Point(0.5, 0.5);
            myLocationOverlay.GeoCoordinate = myGeoCoordinate;

            // Create a MapLayer to contain the MapOverlay.
            MapLayer myLocationLayer = new MapLayer();
            myLocationLayer.Add(myLocationOverlay);

            // create random point
            var rand = new Random();

            for (int i = 0; i < 5; i++)
            {
                // Create a small circle to mark the current location.
                Ellipse myCircle2 = new Ellipse();
                //myCircle2.Fill = new SolidColorBrush(Colors.Blue);
                myCircle2.Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);
                myCircle2.StrokeThickness = 5; 
                myCircle2.Height = 20;
                myCircle2.Width = 20;
                myCircle2.Opacity = 50;

                MapOverlay myLocationOverlay2 = new MapOverlay();
                myLocationOverlay2.Content = myCircle2;
                myLocationOverlay2.PositionOrigin = new Point(0.5, 0.5);
                GeoCoordinate coordinate = new GeoCoordinate(myGeoCoordinate.Latitude + rand.NextDouble() * 0.05 - 0.025, myGeoCoordinate.Longitude + rand.NextDouble() * 0.05 - 0.025);
                myLocationOverlay2.GeoCoordinate = coordinate;

                myLocationLayer.Add(myLocationOverlay2);
            }



            // Add the MapLayer to the Map.
            MyMap.Layers.Add(myLocationLayer);

        }



        private static WriteableBitmap GenerateQRCode(string message)
        {
            BarcodeWriter _writer = new BarcodeWriter();

            _writer.Renderer = new ZXing.Rendering.WriteableBitmapRenderer()
            {
                Foreground = System.Windows.Media.Color.FromArgb(255, 0, 0, 0) // blue
            };

            _writer.Format = BarcodeFormat.QR_CODE;


            _writer.Options.Height = 400;
            _writer.Options.Width = 400;
            _writer.Options.Margin = 1;

            var barcodeImage = _writer.Write(message); //tel: prefix for phone numbers

            return barcodeImage;
        }

        private void btnGenerate(object sender, RoutedEventArgs e)
        {
            imgQRCode.Source = GenerateQRCode("this is my bicycle");
        }


        private async Task TextToSpeech(string textToRead)
        {
            using (var speech = new SpeechSynthesizer())
            {
                await speech.SpeakTextAsync(textToRead);
            }
        }

    }
}