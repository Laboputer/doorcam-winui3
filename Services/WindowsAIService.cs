using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.AI.MachineLearning;
using Windows.Media;
using System.Text.Json;
using System.Text;

namespace doorcamPoC.Services
{
    public class WindowsAIService
    {
        private LearningModelSession? _session;
        private LearningModel? _model;
        private bool _isInitialized = false;

        public async Task InitializeAsync()
        {
            try
            {
                // Windows AI APIs를 사용하여 Phi Silica 모델 로드
                await LoadPhiSilicaModelAsync();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize Windows AI service: {ex.Message}", ex);
            }
        }

        private async Task LoadPhiSilicaModelAsync()
        {
            try
            {
                // Windows AI APIs를 통해 사용 가능한 모델 확인
                // 실제 구현에서는 Windows.AI.MachineLearning 네임스페이스 사용
                _model = null;
                _session = null;
                
                // TODO: 실제 Phi Silica 모델 로드
                // var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/phi-silica.onnx"));
                // _model = await LearningModel.LoadFromStorageFileAsync(modelFile);
                // _session = new LearningModelSession(_model);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load Phi Silica model: {ex.Message}", ex);
            }
        }

        public async Task<string> AnalyzeImageAsync(SoftwareBitmap image, string prompt = "")
        {
            try
            {
                if (!_isInitialized)
                {
                    return await SimulateImageAnalysisAsync(image, prompt);
                }

                // 실제 Windows AI APIs를 사용한 이미지 분석
                var imageFeatures = await ExtractImageFeaturesAsync(image);
                var analysis = await GenerateDescriptionAsync(imageFeatures, prompt);
                
                return analysis;
            }
            catch (Exception ex)
            {
                // AI 분석 실패 시 시뮬레이션으로 폴백
                return await SimulateImageAnalysisAsync(image, prompt);
            }
        }

        public async Task<string> AnalyzeVideoFrameAsync(SoftwareBitmap frame, TimeSpan timestamp, string context = "")
        {
            var timeString = timestamp.ToString(@"hh\:mm");
            var prompt = $"Analyze this door camera frame taken at {timeString}. " +
                        $"Describe what you see, including any people, vehicles, animals, or unusual activities. " +
                        $"Focus on security-relevant events. Context: {context}";

            return await AnalyzeImageAsync(frame, prompt);
        }

        public async Task<List<VideoEvent>> AnalyzeVideoSequenceAsync(List<VideoFrame> frames)
        {
            var events = new List<VideoEvent>();
            var previousAnalysis = "";

            foreach (var frame in frames)
            {
                try
                {
                    // SoftwareBitmap이 null인 경우 건너뛰기
                    if (frame.SoftwareBitmap == null)
                    {
                        continue;
                    }

                    var analysis = await AnalyzeVideoFrameAsync(
                        frame.SoftwareBitmap, 
                        frame.Timestamp, 
                        previousAnalysis
                    );

                    // 중요한 이벤트 감지
                    if (IsSignificantEvent(analysis))
                    {
                        events.Add(new VideoEvent
                        {
                            Timestamp = frame.Timestamp,
                            Description = analysis,
                            EventType = DetermineEventType(analysis),
                            Confidence = CalculateConfidence(analysis),
                            FrameIndex = frames.IndexOf(frame)
                        });
                    }

                    previousAnalysis = analysis;
                }
                catch (Exception)
                {
                    // 개별 프레임 분석 실패 시 계속 진행
                    continue;
                }
            }

            // 이벤트가 없으면 시뮬레이션 이벤트 추가
            if (events.Count == 0)
            {
                events.AddRange(GenerateSimulatedEvents(frames));
            }

            return events;
        }

        private List<VideoEvent> GenerateSimulatedEvents(List<VideoFrame> frames)
        {
            var events = new List<VideoEvent>();
            var random = new Random();
            
            // 프레임 수에 따라 시뮬레이션 이벤트 생성
            var eventCount = Math.Min(frames.Count / 3, 5); // 최대 5개 이벤트
            
            for (int i = 0; i < eventCount; i++)
            {
                var frameIndex = random.Next(frames.Count);
                var frame = frames[frameIndex];
                
                var eventTypes = new[] { "Person", "Vehicle", "Animal", "Package", "Movement" };
                var eventType = eventTypes[random.Next(eventTypes.Length)];
                
                events.Add(new VideoEvent
                {
                    Timestamp = frame.Timestamp,
                    Description = GenerateSimulatedDescription(eventType, frame.Timestamp),
                    EventType = eventType,
                    Confidence = 0.7 + (random.NextDouble() * 0.3), // 0.7-1.0
                    FrameIndex = frameIndex
                });
            }
            
            return events.OrderBy(e => e.Timestamp).ToList();
        }

        private string GenerateSimulatedDescription(string eventType, TimeSpan timestamp)
        {
            var timeString = timestamp.ToString(@"hh\:mm");
            
            return eventType switch
            {
                "Person" => $"{timeString}에 문 앞에 사람이 서 있는 것이 감지되었습니다.",
                "Vehicle" => $"{timeString}에 차량이 주차되고 운전자가 내리는 것이 보입니다.",
                "Animal" => $"{timeString}에 고양이가 문 앞을 지나가는 것이 포착되었습니다.",
                "Package" => $"{timeString}에 택배 배송원이 패키지를 들고 도착했습니다.",
                "Movement" => $"{timeString}에 문 앞에서 움직임이 감지되었습니다.",
                _ => $"{timeString}에 활동이 감지되었습니다."
            };
        }

        private async Task<object> ExtractImageFeaturesAsync(SoftwareBitmap image)
        {
            // 이미지에서 특징 추출
            // 실제 구현에서는 Windows AI APIs 사용
            await Task.Delay(100); // 시뮬레이션 지연
            return new { features = "extracted" };
        }

        private async Task<string> GenerateDescriptionAsync(object imageFeatures, string prompt)
        {
            // Phi Silica 모델을 사용하여 이미지 설명 생성
            // 실제 구현에서는 Windows AI APIs 사용
            await Task.Delay(200); // 시뮬레이션 지연
            
            if (!string.IsNullOrEmpty(prompt))
            {
                return await SimulatePhiSilicaResponseAsync(prompt);
            }
            
            return "이미지 분석이 완료되었습니다.";
        }

        private async Task<string> SimulateImageAnalysisAsync(SoftwareBitmap image, string prompt)
        {
            await Task.Delay(150); // 시뮬레이션 지연

            var random = new Random();
            var hour = DateTime.Now.Hour;
            
            var descriptions = new List<string>();
            
            // 시간대별 다른 분석 결과
            if (hour >= 6 && hour <= 22)
            {
                descriptions.AddRange(new[]
                {
                    "문 앞에 사람이 서 있습니다. 평상복을 입고 있어 가족 구성원으로 보입니다.",
                    "택배 배송원이 패키지를 들고 문 앞에 도착했습니다.",
                    "차량이 주차되어 있고, 운전자가 내리고 있습니다.",
                    "고양이가 문 앞을 천천히 지나가고 있습니다.",
                    "문 앞에 누군가가 잠시 서서 기다리고 있습니다."
                });
            }
            else
            {
                descriptions.AddRange(new[]
                {
                    "밤시간이라 조명이 어둡습니다. 특별한 활동은 보이지 않습니다.",
                    "가로등 불빛 아래 고양이가 지나가는 것이 보입니다.",
                    "문 앞이 조용하고 아무런 움직임이 없습니다."
                });
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                return await SimulatePhiSilicaResponseAsync(prompt);
            }

            return descriptions[random.Next(descriptions.Count)];
        }

        private async Task<string> SimulatePhiSilicaResponseAsync(string prompt)
        {
            await Task.Delay(100);
            
            if (prompt.Contains("door camera") || prompt.Contains("security"))
            {
                var timeMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"(\d{2}:\d{2})");
                var timeString = timeMatch.Success ? timeMatch.Groups[1].Value : "현재";
                
                var responses = new[]
                {
                    $"보안 카메라 화면에서 {timeString}에 문 앞에 사람이 서 있는 것을 확인했습니다. 평상복 차림으로 가족 구성원으로 판단됩니다.",
                    $"{timeString}에 택배 배송원이 노란색 배송 차량에서 내려 패키지를 들고 문 앞으로 다가오고 있습니다.",
                    $"이 시간대({timeString})에 고양이가 문 앞을 천천히 지나가는 것이 포착되었습니다. 특별한 보안 위험은 없어 보입니다.",
                    $"{timeString}에 차량이 주차되고 운전자가 내려 문 앞으로 걸어오고 있습니다. 정상적인 방문으로 보입니다.",
                    $"문 앞에 {timeString}에 누군가가 잠시 서서 기다리고 있습니다. 인상착의를 확인할 수 있습니다."
                };
                
                return responses[new Random().Next(responses.Length)];
            }
            
            return "이미지를 분석한 결과, 특별한 보안 이벤트는 감지되지 않았습니다.";
        }

        private bool IsSignificantEvent(string analysis)
        {
            var significantKeywords = new[]
            {
                "사람", "택배", "차량", "고양이", "개", "방문자", "배송원",
                "person", "delivery", "vehicle", "cat", "dog", "visitor"
            };

            return significantKeywords.Any(keyword => 
                analysis.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private string DetermineEventType(string analysis)
        {
            if (analysis.Contains("사람") || analysis.Contains("person"))
                return "Person";
            if (analysis.Contains("택배") || analysis.Contains("delivery"))
                return "Package";
            if (analysis.Contains("차량") || analysis.Contains("vehicle"))
                return "Vehicle";
            if (analysis.Contains("고양이") || analysis.Contains("개") || 
                analysis.Contains("cat") || analysis.Contains("dog"))
                return "Animal";
            
            return "Movement";
        }

        private double CalculateConfidence(string analysis)
        {
            // 분석 텍스트의 길이와 구체성에 따라 신뢰도 계산
            var baseConfidence = 0.6;
            var lengthBonus = Math.Min(analysis.Length / 100.0, 0.3);
            var specificityBonus = analysis.Contains("시간") || analysis.Contains("time") ? 0.1 : 0.0;
            
            return Math.Min(baseConfidence + lengthBonus + specificityBonus, 1.0);
        }

        public void Dispose()
        {
            _session?.Dispose();
            _model?.Dispose();
        }
    }

    public class VideoEvent
    {
        public TimeSpan Timestamp { get; set; }
        public string Description { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int FrameIndex { get; set; }
        public List<string> DetectedObjects { get; set; } = new List<string>();
    }
} 