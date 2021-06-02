using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System;
using System.Collections.Generic;
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

namespace VoiceRecognition
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SpeechConfig speechConfig = null;
        SpeechRecognizer recognizer = null;
        TextAnalyticsClient textAnalyticsClient = null;

        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            speechConfig = SpeechConfig.FromSubscription(Properties.Settings.Default.SpeechTextAPIKey, Properties.Settings.Default.SpeechTextRegion);
            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            textAnalyticsClient = new TextAnalyticsClient(new Uri(Properties.Settings.Default.TextAnalyticsEndPoint), new AzureKeyCredential(Properties.Settings.Default.TextAnalyticsAPIKey));
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {            
            bool isReadFile = rbTrue.IsChecked.HasValue ? rbTrue.IsChecked.Value : false;

            if (isReadFile)
            {
                await ReadFile();
            }
            else
            {
                await StartListen().ConfigureAwait(false);
            }

        }

        private void AnalyseConversation()
        {
            string document = txtScript.Text;
            StringBuilder stringBuilder = new StringBuilder();

            try
            {

                Response<CategorizedEntityCollection> response = textAnalyticsClient.RecognizeEntities(document);
                CategorizedEntityCollection entitiesInDocument = response.Value;

                Console.WriteLine($"Recognized {entitiesInDocument.Count} entities:");
                foreach (CategorizedEntity entity in entitiesInDocument)
                {
                    stringBuilder.AppendLine($"Text: {entity.Text}");
                    stringBuilder.AppendLine($"Category: {entity.Category}");
                    if (!string.IsNullOrEmpty(entity.SubCategory))
                        stringBuilder.AppendLine($"SubCategory: {entity.SubCategory}");
                    stringBuilder.AppendLine($"Confidence score: {entity.ConfidenceScore}");
                    stringBuilder.AppendLine("");
                }

                Dispatcher.Invoke(() =>
                {
                    txtAnalyticsResult.Text = stringBuilder.ToString();
                });
            }
            catch (RequestFailedException exception)
            {
                Dispatcher.Invoke(() =>
                {
                    txtAnalyticsResult.Text = exception.Message;
                });
            }
        }

        private async Task ReadFile()
        {
            var reader = new BinaryReader(File.OpenRead(Properties.Settings.Default.SpeechAudioFilePath));
            var audioInputStream = AudioInputStream.CreatePushStream();
            var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            byte[] readBytes;
            do
            {
                readBytes = reader.ReadBytes(1024);
                audioInputStream.Write(readBytes, readBytes.Length);
            } while (readBytes.Length > 0);

            var result = await recognizer.RecognizeOnceAsync();

            Dispatcher.Invoke(() =>
            {
                txtScript.Text = result.Text;
            });

        }

        private async Task StartListen()
        {
            var stopRecognition = new TaskCompletionSource<int>();

            recognizer.Recognizing += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    txtScript.Text = e.Result.Text;
                });
            };

            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtScript.Text = e.Result.Text;
                        AnalyseConversation();
                    });                    
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtScript.Text = "Speech could not be recognized.";
                    });
                }
            };

            recognizer.Canceled += (s, e) =>
            {
                Console.WriteLine($"CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtScript.Text = $"CANCELED: ErrorDetails={e.ErrorDetails}";
                    });

                }

                stopRecognition.TrySetResult(0);
            };

            recognizer.SessionStopped += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    txtScript.Text = "Session stopped event.";
                });

                stopRecognition.TrySetResult(0);
            };

            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            Task.WaitAny(new[] { stopRecognition.Task });

            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }
    }
}
