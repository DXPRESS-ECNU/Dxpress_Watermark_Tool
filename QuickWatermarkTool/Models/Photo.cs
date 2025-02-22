﻿using Avalonia.Controls;
using ReactiveUI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Image = SixLabors.ImageSharp.Image;
using Point = SixLabors.Primitives.Point;
using Size = SixLabors.Primitives.Size;

namespace QuickWatermarkTool.Models
{
    public class Photo : ReactiveObject
    {
        public static ObservableCollection<Photo> Photos => Program.MwDataContext.Photos;

        public static string SavingPath
        {
            get => Program.MwDataContext.SavingPath;
            set
            {
                Program.MwDataContext.SavingPath = value; 
                Log.Information($"Save to {value}.");
            }
        }

        public string CurrentPhotoSavingPath
        {
            get
            {
                if (string.IsNullOrEmpty(SavingPath))
                {
                    return Path.GetDirectoryName(ImagePath);
                }

                return SavingPath;
            }
        }

        public static Format SavingFormat => (Format)Enum.Parse(typeof(Format),Program.MwDataContext.SelectedSavingFormat.ToLower());

        private Image<Rgba32> originImage;
        private Image<Rgba32> watermarkImage;

        public string ImagePath { get; set; }

        public string FileName { get; }
        private string _status;

        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public int Width => originImage.Width;

        public int Height => originImage.Height;

        private int WmHeight => watermarkImage.Height;
        private int WmWidth => watermarkImage.Width;

        public int FrameCount => originImage.Frames.Count;

        public Photo(string path)
        {
            this.ImagePath = path;
            FileName = Path.GetFileName(ImagePath);
            Status = "Loaded";
            Log.Information($"{path} loaded.");
        }

        public void Watermark()
        {
            Status = "Starting";
            Log.Information($"{this.ImagePath} started.");
            originImage = Image.Load(ImagePath);
            string wmpath = Config.config.WatermarkFilename;
            watermarkImage = Image.Load(wmpath);

            if (Width > Config.config.MaxOutputImageWidth || Height > Config.config.MaxOutputImageHeight)
                ResizePic(originImage, Config.config.MaxOutputImageWidth, Config.config.MaxOutputImageHeight);

            int wmPosiW, wmPosiH;
            int maxWmWidth = (int)Math.Floor(Config.config.MaxWatermarkScaleWidth * Width);
            int maxWmHeight = (int)Math.Floor(Config.config.MaxWatermarkScaleHeight * Height);

            ResizePic(watermarkImage, maxWmWidth, maxWmHeight);

            int offsetW, offsetH;
            offsetW = Config.config.WatermarkOffsetWidth;
            offsetH = Config.config.WatermarkOffsetHeight;

            offsetW = offsetW * Width / Config.config.MaxOutputImageWidth;

            offsetH = offsetW; // Only for DXPRESS.

            switch (Config.config.WatermarkPosition)
            {
                case WatermarkPosition.LeftBottom:
                    wmPosiW = offsetW;
                    wmPosiH = Height - WmHeight - offsetH;
                    break;
                case WatermarkPosition.LeftTop:
                    wmPosiW = offsetW;
                    wmPosiH = offsetH;
                    break;
                case WatermarkPosition.RightBottom:
                    wmPosiW = Width - WmWidth - offsetW;
                    wmPosiH = Height - WmHeight - offsetH;
                    break;
                case WatermarkPosition.RightTop:
                    wmPosiW = Width - WmWidth - offsetW;
                    wmPosiH = offsetH;
                    break;
                case WatermarkPosition.Center:
                    wmPosiW = (Width - WmWidth) / 2;
                    wmPosiH = (Height - WmHeight) / 2;
                    break;
                case WatermarkPosition.BottomMiddle:
                    wmPosiW = (Width - WmWidth) / 2;
                    wmPosiH = Height - offsetH - WmHeight;
                    break;
                case WatermarkPosition.TopMiddle:
                    wmPosiW = (Width - WmWidth) / 2;
                    wmPosiH = offsetH;
                    break;
                default:
                    throw new NotImplementedException();
            }
            originImage.Mutate(i => { i.DrawImage(watermarkImage, new Point(wmPosiW, wmPosiH), Config.config.WatermarkOpacity); });
        }

        public void AddCopyright()
        {
            var newExifProfile = originImage.MetaData.ExifProfile == null ? new ExifProfile() : new ExifProfile(originImage.MetaData.ExifProfile.ToByteArray());
            if (Config.config.Copyright != "")
            {
                newExifProfile.SetValue(ExifTag.Copyright, Config.config.Copyright);
            }

            if (Config.config.AuthorName != "")
            {
                newExifProfile.SetValue(ExifTag.Artist, Config.config.AuthorName);
            }
            originImage.MetaData.ExifProfile = newExifProfile;
        }

        private static void ResizePic(Image<Rgba32> image, int maxWidth, int maxHeight)
        {
            ResizeOptions resizeOptions = new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxWidth, maxHeight)
            };
            image.Mutate(x => x.Resize(resizeOptions));
        }

        public void SaveImage()
        {
            if (FrameCount > 1)
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(ImagePath);
                string saveName = fileNameWithoutExt + Config.config.OutputSuffix + ".gif";
                string filepath = Path.Combine(CurrentPhotoSavingPath, saveName);
                originImage.Save(filepath, new GifEncoder());
            }
            else
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(ImagePath);
                string saveName = fileNameWithoutExt + Config.config.OutputSuffix + "." +
                                  Enum.GetName(typeof(Format), SavingFormat);
                string filepath = Path.Combine(CurrentPhotoSavingPath, saveName);
                switch (SavingFormat)
                {
                    case Format.png:
                        originImage.Save(filepath, new PngEncoder());
                        break;
                    case Format.gif:
                        originImage.Save(filepath, new GifEncoder());
                        break;
                    case Format.jpg:
                        originImage.Save(filepath, new JpegEncoder
                        {
                            Quality = 80
                        });
                        break;
                }
            }

            originImage.Dispose();
            watermarkImage.Dispose();
            Status = "Success";
            Log.Information($"{this.ImagePath} success.");
        }

        public static async Task SelectPhotoFiles()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select Photos",
                AllowMultiple = true
            };
            FileDialogFilter imageFilter = new FileDialogFilter();
            imageFilter.Extensions.AddRange(new[] { "jpg", "jpeg", "png", "gif" });
            imageFilter.Name = "Images";
            dialog.Filters.Add(imageFilter);
            string[] files = await dialog.ShowAsync(Program.MainWindow);
            foreach (var file in files)
            {
                string filedDecode = System.Web.HttpUtility.UrlDecode(file);
                if (Photos.Count(i => i.ImagePath == filedDecode) == 0)
                    Photos.Add(new Photo(filedDecode));
            }
        }

        public static async Task SelectSavingFolder()
        {
            OpenFolderDialog ofd = new OpenFolderDialog
            {
                Title = "Select Saving Folder"
            };
            string folder = await ofd.ShowAsync(Program.MainWindow);
            if (!string.IsNullOrEmpty(folder))
                SavingPath = System.Web.HttpUtility.UrlDecode(folder);
        }

        public enum Format
        {
            jpg,
            png,
            gif
        }
        public enum WatermarkPosition
        {
            LeftTop,
            LeftBottom,
            RightTop,
            RightBottom,
            TopMiddle,
            BottomMiddle,
            Center
        }
    }
}
