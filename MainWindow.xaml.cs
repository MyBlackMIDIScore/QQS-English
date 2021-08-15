﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using QQS_UI.Core;
using Path = System.IO.Path;
using System.Diagnostics;
using System.Threading;

namespace QQS_UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private RenderFile file = null;
        private bool isLoading = false;
        private Core.RenderOptions options = Core.RenderOptions.CreateRenderOptions();
        private CommonRenderer renderer = null;
        private readonly Config config;
        private readonly CustomColor customColors;
        private const string DefaultVideoFilter = "Video (*.mp4, *.avi, *.mov)|*.mp4;*.avi;*.mov",
            PNGVideoFilter = "Video (*.mp4, *.mov)|*.mp4, *.mov",
            TransparentVideoFilter = "Video (*.mov)|*.mov";
        public MainWindow()
        {
            InitializeComponent();
            config = new Config();
            customColors = new CustomColor();
            if (config.CachedMIDIDirectory == null)
            {
                config.CachedMIDIDirectory = new OpenFileDialog().InitialDirectory;
            }
            if (config.CachedVideoDirectory == null)
            {
                config.CachedVideoDirectory = new SaveFileDialog().InitialDirectory;
            }
            if (config.CachedColorDirectory == null)
            {
                config.CachedColorDirectory = config.CachedVideoDirectory;
            }
            config.SaveConfig();
            previewColor.Background = new SolidColorBrush(new Color
            {
                R = (byte)(options.DivideBarColor & 0xff),
                G = (byte)((options.DivideBarColor & 0xff00) >> 8),
                B = (byte)((options.DivideBarColor & 0xff0000) >> 16),
                A = 0xff
            });
            previewBackgroundColor.Background = new SolidColorBrush(new Color
            {
                R = 0,
                G = 0,
                B = 0,
                A = 255
            });

            if (!PFAConfigrationLoader.IsConfigurationAvailable)
            {
                loadPFAColors.IsEnabled = false;
            }
#if DEBUG
            Title += " (Debug)";
#endif
        }

        private void openMidi_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "MIDI File (*.mid)|*.mid",
                InitialDirectory = config.CachedMIDIDirectory
            };
            if ((bool)dialog.ShowDialog())
            {
                string midiDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                config.CachedMIDIDirectory = midiDirectory;
                midiPath.Text = dialog.FileName;
                config.SaveConfig();
            }
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading)
            {
                return;
            }
            string fileName = midiPath.Text;
            if (!File.Exists(fileName) || !fileName.EndsWith(".mid"))
            {
                _ = MessageBox.Show("Incorrect path.", "Unable to load the MIDI.");
                return;
            }
            trackCount.Content = "Loading...";
            noteCount.Content = "Loading...";
            _ = Task.Run(() =>
            {
                isLoading = true;
                file = new RenderFile(fileName);
                isLoading = false;
                TimeSpan midilen = Global.GetTimeOf(file.MidiTime, file.Division, file.Tempos);
                Dispatcher.Invoke(() =>
                {
                    Resources["midiLoaded"] = true;
                    trackCount.Content = file.TrackCount.ToString();
                    noteCount.Content = file.NoteCount.ToString();
                    midiLen.Content = midilen.ToString("mm\\:ss\\.fff");
                });
            });
        }

        private void unloadButton_Click(object sender, RoutedEventArgs e)
        {
            int gen = GC.GetGeneration(file);
            file = null;
            GC.Collect(gen);
            Resources["midiLoaded"] = false;
            Console.WriteLine("MIDI Unloaded.");
            noteCount.Content = "-";
            trackCount.Content = "-";
            midiLen.Content = "--:--.---";
        }

        private void fpsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (fpsBox.SelectedIndex)
            {
                case 0:
                    options.FPS = 30;
                    break;
                case 1:
                    options.FPS = 60;
                    break;
                case 2:
                    options.FPS = 120;
                    break;
                default:
                    options.FPS = 240;
                    break;
            }
        }

        private void noteSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            options.NoteSpeed = noteSpeed.Value;
        }

        private void renderResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (renderResolution.SelectedIndex)
            {
                case 0:
                    options.Width = 640;
                    options.Height = 480;
                    break;
                case 1:
                    options.Width = 1280;
                    options.Height = 720;
                    break;
                case 2:
                    options.Width = 1920;
                    options.Height = 1080;
                    break;
                case 3:
                    options.Width = 2560;
                    options.Height = 1440;
                    break;
                case 4:
                    options.Width = 3840;
                    options.Height = 2160;
                    break;
                default:
                    break;
            }
            options.KeyHeight = options.Height * 15 / 100;
        }

        private void selectOutput_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Filter = options.TransparentBackground ? TransparentVideoFilter : (options.PNGEncoder ? PNGVideoFilter : DefaultVideoFilter),
                Title = "Video output path.",
                InitialDirectory = config.CachedVideoDirectory
            };
            if ((bool)dialog.ShowDialog())
            {
                config.CachedVideoDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                outputPath.Text = dialog.FileName;
                config.SaveConfig();
            }
        }

        private void startRender_Click(object sender, RoutedEventArgs e)
        {
            if (file == null)
            {
                _ = MessageBox.Show("Unable to render: \nNo MIDI file loaded.", "No MIDI file loaded.");
                return;
            }
            options.Input = midiPath.Text;
            options.Output = outputPath.Text;
            options.PreviewMode = false;
            options.AdditionalFFMpegArgument = additionalFFArgs.Text;
            Resources["notRendering"] = Resources["notRenderingOrPreviewing"] = false;
            renderer = new CommonRenderer(file, options);
            _ = Task.Run(() =>
            {
                Console.WriteLine("Rendering...");
                renderer.Render();
                int gen = GC.GetGeneration(renderer);
                Dispatcher.Invoke(() =>
                {
                    renderer = null;
                    Resources["notRendering"] = Resources["notRenderingOrPreviewing"] = true;
                });
                GC.Collect(gen);
            });
        }

        private void interruptButton_Click(object sender, RoutedEventArgs e)
        {
            if (renderer != null)
            {
                renderer.Interrupt = true;
            }
        }

        private void crfSelect_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            options.CRF = (int)crfSelect.Value;
        }

        private void enableTranparentBackground_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            options.TransparentBackground = enableTranparentBackground.IsChecked;
            if (options.TransparentBackground)
            {
                if (!outputPath.Text.EndsWith(".mov"))
                {
                    outputPath.Text = outputPath.Text.Substring(0, outputPath.Text.Length - 4) + ".mov";
                }
            }
        }
        private void startPreview_Click(object sender, RoutedEventArgs e)
        {
            if (file == null)
            {
                _ = MessageBox.Show("Unable to preview: \nNo MIDI file loaded.", "No MIDI file loaded.");
                return;
            }
            if (usePNGEncoder.IsChecked)
            {
                _ = MessageBox.Show("Unable to preview: \nPreview is not supported with PNG sequence.", "Unable to preview.");
                return;
            }
            options.Input = midiPath.Text;
            options.Output = outputPath.Text;
            options.PreviewMode = true;
            options.AdditionalFFMpegArgument = additionalFFArgs.Text;
            Resources["notPreviewing"] = Resources["notRenderingOrPreviewing"] = false;
            renderer = new CommonRenderer(file, options);
            _ = Task.Run(() =>
            {
                Console.WriteLine("Previewing...");
                renderer.Render();
                int gen = GC.GetGeneration(renderer);
                Dispatcher.Invoke(() =>
                {
                    renderer = null;
                    Resources["notPreviewing"] = Resources["notRenderingOrPreviewing"] = true;
                });
                GC.Collect(gen);
            });
        }

        private void useDefaultColors_Click(object sender, RoutedEventArgs e)
        {
            customColors.UseDefault();
            customColors.SetGlobal();
            _ = MessageBox.Show("Default colours set.", "Default colours set");
        }

        private void loadColors_Click(object sender, RoutedEventArgs e)
        {
            string filePath = colorPath.Text;
            if (!filePath.EndsWith(".json"))
            {
                _ = MessageBox.Show("Unable to load palette.\nPalettes are only supported in JSON format..", "Unable to load palette");
                return;
            }
            if (!File.Exists(filePath))
            {
                _ = MessageBox.Show("Unable to load palette: Palette does not exist.", "Unable to load palette");
                return;
            }
            int errCode = customColors.Load(filePath);
            if (errCode == 1)
            {
                _ = MessageBox.Show("Unable to load palette: This file format is not supported.", "Unable to load palette");
                return;
            }
            errCode = customColors.SetGlobal();
            if (errCode != 0)
            {
                _ = MessageBox.Show("Unable to load palette: Palette is empty.", "Unable to load palette");
                return;
            }
            _ = MessageBox.Show("Palette loaded: " + customColors.Colors.Length + " colours.", "Palette loaded");
        }

        private void openColorFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "JSON file (*.json)|*.json",
                InitialDirectory = config.CachedColorDirectory
            };
            if ((bool)dialog.ShowDialog())
            {
                string colorDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                config.CachedColorDirectory = colorDirectory;
                colorPath.Text = dialog.FileName;
                config.SaveConfig();
            }
        }

        private void enableRandomColor_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            if (enableRandomColor.IsChecked)
            {
                _ = customColors.Shuffle().SetGlobal();
            }
            else
            {
                _ = customColors.SetGlobal();
            }
        }

        private void limitPreviewFPS_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            Global.LimitPreviewFPS = e.NewValue;
        }

        private void loadPFAColors_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RGBAColor[] colors = PFAConfigrationLoader.LoadPFAConfigurationColors();
                customColors.Colors = colors;
                customColors.SetGlobal();
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"加载 PFA 配置颜色时出现了错误: \n{ex.Message}\n栈追踪: \n{ex.StackTrace}", "无法加载 PFA 配置");
            }
        }

        private void setbgColor_Click(object sender, RoutedEventArgs e)
        {
            string coltxt = bgColor.Text;
            if (coltxt.Length != 6)
            {
                _ = MessageBox.Show("当前的颜色代码不符合规范.\n一个颜色代码应当由6位16进制表示的数字组成.", "无法设置颜色");
                return;
            }
            try
            {
                byte r = Convert.ToByte(coltxt.Substring(0, 2), 16);
                byte g = Convert.ToByte(coltxt.Substring(2, 2), 16);
                byte b = Convert.ToByte(coltxt.Substring(4, 2), 16);
                uint col = 0xff000000U | r | (uint)(g << 8) | (uint)(b << 16);
                options.BackgroundColor = col;
                previewBackgroundColor.Background = new SolidColorBrush(new Color()
                {
                    R = r,
                    G = g,
                    B = b,
                    A = 0xff
                });
            }
            catch
            {
                _ = MessageBox.Show("错误: 无法解析颜色代码.\n请检查输入的颜色代码是否正确.", "无法设置颜色");
            }
        }

        private void setBarColor_Click(object sender, RoutedEventArgs e)
        {
            string coltxt = barColor.Text;
            if (coltxt.Length != 6)
            {
                _ = MessageBox.Show("Incorrect colour code.\nPlease enter the colour in hexadecimal format.", "Incorrect colour code");
                return;
            }
            try
            {
                byte r = Convert.ToByte(coltxt.Substring(0, 2), 16);
                byte g = Convert.ToByte(coltxt.Substring(2, 2), 16);
                byte b = Convert.ToByte(coltxt.Substring(4, 2), 16);
                uint col = 0xff000000U | r | (uint)(g << 8) | (uint)(b << 16);
                options.DivideBarColor = col;
                previewColor.Background = new SolidColorBrush(new Color()
                {
                    R = r,
                    G = g,
                    B = b,
                    A = 0xff
                });
            }
            catch
            {
                _ = MessageBox.Show("Incorrect colour code: The colour code is invalid.", "Incorrect colour code");
            }
        }

        private void usePNGEncoder_CheckToggled(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            if (outputPath.Text != null)
            {
                if (outputPath.Text.EndsWith(".avi") && e.NewValue)
                {
                    _ = MessageBox.Show("Note: AVI is not supported as a PNG sequence encoder.\nPlease save as MP4 or MOV.", "Cannot be set as a PNG sequence encoder");
                    e.Handled = true;
                    usePNGEncoder.IsChecked = false;
                    return;
                }
            }
            options.PNGEncoder = usePNGEncoder.IsChecked;
            if (!options.PNGEncoder)
            {
                enableTranparentBackground.IsChecked = false;
                options.TransparentBackground = false;
            }
        }
    }

    internal class NotValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    internal class AndValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = true;
            foreach (object obj in values)
            {
                b &= (bool)obj;
            }
            return b;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
