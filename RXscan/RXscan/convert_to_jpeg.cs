using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace RXscan
{
    class convert_to_jpeg
    {

        /// <summary>
        /// Converts source image file to jpeg of defined quality (0.85)
        /// </summary>
        /// <param name="sourceFile">Source StorageFile</param>
        /// <param name="outputFile">Target StorageFile</param>
        /// <returns></returns>
        public async Task<StorageFile> ConvertImageToJpegAsync(StorageFile sourceFile, StorageFile outputFile)
        {
            //you can use WinRTXamlToolkit StorageItemExtensions.GetSizeAsync to get file size (if you already plugged this nuget in)
            var sourceFileProperties = await sourceFile.GetBasicPropertiesAsync();
            var fileSize = sourceFileProperties.Size;
            var imageStream = await sourceFile.OpenReadAsync();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            using (imageStream)
            {
                var decoder = await BitmapDecoder.CreateAsync(imageStream);
                var pixelData = await decoder.GetPixelDataAsync();
                var detachedPixelData = pixelData.DetachPixelData();
                pixelData = null;
                
                // Set image quality
                double jpegImageQuality = 0.80d;
                Debug.WriteLine("Source image size: " + fileSize);

                var imageWriteableStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
                ulong jpegImageSize = 0;
                using (imageWriteableStream)
                {
                    var propertySet = new BitmapPropertySet();
                    var qualityValue = new BitmapTypedValue(jpegImageQuality, Windows.Foundation.PropertyType.Single);
                    propertySet.Add("ImageQuality", qualityValue);
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, imageWriteableStream, propertySet);
                    //key thing here is to use decoder.OrientedPixelWidth and decoder.OrientedPixelHeight otherwise you will get garbled image on devices on some photos with orientation in metadata
                    encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, decoder.DpiX, decoder.DpiY, detachedPixelData);
                    await encoder.FlushAsync();
                    await imageWriteableStream.FlushAsync();
                    jpegImageSize = imageWriteableStream.Size;
                }
                Debug.WriteLine("Final image size now: " + jpegImageSize);
            }
            stopwatch.Stop();
            Debug.WriteLine("Time spent optimizing image: " + stopwatch.Elapsed);
            return outputFile;
        }
    }
}
