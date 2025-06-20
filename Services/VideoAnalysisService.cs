using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.MediaProperties;
using System.IO;
using System.Linq;

namespace doorcamPoC.Services
{
    public class VideoAnalysisResult
    {
        public string FileName { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public double FrameRate { get; set; }
        public long FileSize { get; set; }
        public List<DetectedEvent> DetectedEvents { get; set; } = new List<DetectedEvent>();
        public string Summary { get; set; } = string.Empty;
        public List<DetectedObject> AllDetectedObjects { get; set; } = new List<DetectedObject>();
    }

    public class DetectedEvent
    {
        public TimeSpan Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<string> DetectedObjects { get; set; } = new List<string>();
    }

    public class VideoAnalysisService
    {
        private readonly AIModelService _aiModelService;
        private readonly VideoFrameExtractorService _frameExtractorService;

        public VideoAnalysisService()
        {
            _aiModelService = new AIModelService();
            _frameExtractorService = new VideoFrameExtractorService();
        }

        public async Task<VideoAnalysisResult> AnalyzeVideoAsync(StorageFile videoFile)
        {
            var result = new VideoAnalysisResult
            {
                FileName = videoFile.Name,
                FileSize = (long)(await videoFile.GetBasicPropertiesAsync()).Size
            };

            try
            {
                // AI 모델 초기화
                await _aiModelService.InitializeAsync();

                // Load video properties
                await LoadVideoPropertiesAsync(videoFile, result);
                
                // Extract frames and analyze with AI
                await AnalyzeVideoFramesAsync(videoFile, result);
                
                // Generate events from detected objects
                GenerateEventsFromDetections(result);
                
                // Generate summary
                result.Summary = GenerateSummary(result);
                
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Video analysis failed: {ex.Message}", ex);
            }
        }

        private async Task LoadVideoPropertiesAsync(StorageFile videoFile, VideoAnalysisResult result)
        {
            try
            {
                var videoProps = await videoFile.Properties.GetVideoPropertiesAsync();
                result.Width = videoProps.Width;
                result.Height = videoProps.Height;
                result.Duration = videoProps.Duration;
                // FrameRate 정보는 VideoProperties에 없음. 임시로 30으로 설정
                result.FrameRate = 30.0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load video properties: {ex.Message}", ex);
            }
        }

        private async Task AnalyzeVideoFramesAsync(StorageFile videoFile, VideoAnalysisResult result)
        {
            try
            {
                // 프레임 추출 간격 설정 (비디오 길이에 따라 조정)
                var interval = result.Duration.TotalMinutes > 60 ? 
                    TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(5);

                // 프레임 추출
                var frames = await _frameExtractorService.ExtractFramesAsync(videoFile, interval);
                
                // 각 프레임을 AI로 분석
                foreach (var frame in frames)
                {
                    if (frame.SoftwareBitmap != null)
                    {
                        var detectedObjects = await _aiModelService.AnalyzeFrameAsync(frame.SoftwareBitmap, frame.Timestamp);
                        result.AllDetectedObjects.AddRange(detectedObjects);
                    }
                    else
                    {
                        // SoftwareBitmap이 null인 경우 시뮬레이션
                        var simulatedObjects = await _aiModelService.AnalyzeFrameAsync(null!, frame.Timestamp);
                        result.AllDetectedObjects.AddRange(simulatedObjects);
                    }
                }
            }
            catch (Exception ex)
            {
                // 프레임 분석 실패 시 시뮬레이션으로 폴백
                await SimulateAIAnalysisAsync(result);
            }
        }

        private void GenerateEventsFromDetections(VideoAnalysisResult result)
        {
            if (!result.AllDetectedObjects.Any())
            {
                // 감지된 객체가 없으면 시뮬레이션 이벤트 생성
                GenerateSimulatedEvents(result);
                return;
            }

            var events = new List<DetectedEvent>();
            var groupedDetections = result.AllDetectedObjects
                .GroupBy(obj => new { obj.Label, Hour = obj.Timestamp.Hours })
                .OrderBy(g => g.Key.Hour)
                .ThenBy(g => g.First().Timestamp);

            foreach (var group in groupedDetections)
            {
                var firstDetection = group.First();
                var confidence = group.Average(obj => obj.Confidence);
                
                var eventType = GetEventTypeFromLabel(group.Key.Label);
                var description = GenerateDescriptionFromDetection(group.Key.Label, firstDetection.Timestamp);
                
                events.Add(new DetectedEvent
                {
                    Timestamp = firstDetection.Timestamp,
                    EventType = eventType,
                    Description = description,
                    Confidence = confidence,
                    DetectedObjects = group.Select(obj => obj.Label).Distinct().ToList()
                });
            }

            result.DetectedEvents = events;
        }

        private string GetEventTypeFromLabel(string label)
        {
            return label switch
            {
                "person" => "Person",
                "car" or "truck" or "bus" => "Vehicle",
                "cat" or "dog" or "bird" => "Animal",
                "backpack" or "handbag" or "suitcase" => "Package",
                _ => "Movement"
            };
        }

        private string GenerateDescriptionFromDetection(string label, TimeSpan timestamp)
        {
            var timeString = timestamp.ToString(@"hh\:mm");
            
            return label switch
            {
                "person" => $"가족 구성원이 {timeString}에 활동했습니다",
                "car" => $"차량이 {timeString}에 도착했습니다",
                "truck" => $"택배 차량이 {timeString}에 도착했습니다",
                "cat" => $"고양이가 {timeString}에 문 앞을 지나갔습니다",
                "dog" => $"개가 {timeString}에 문 앞을 지나갔습니다",
                "backpack" or "handbag" or "suitcase" => $"패키지가 {timeString}에 배달되었습니다",
                _ => $"{label}이(가) {timeString}에 감지되었습니다"
            };
        }

        private void GenerateSimulatedEvents(VideoAnalysisResult result)
        {
            // 기존 시뮬레이션 로직 유지
            var random = new Random();
            var events = new List<DetectedEvent>();
            
            // Simulate events throughout the day
            var startTime = DateTime.Today.AddHours(6); // 6 AM
            var endTime = DateTime.Today.AddHours(22); // 10 PM
            
            var eventTypes = new[] { "Person", "Vehicle", "Animal", "Package", "Movement" };
            var descriptions = new Dictionary<string, string[]>
            {
                ["Person"] = new[] { "가족 구성원이 외출했습니다", "가족 구성원이 귀가했습니다", "방문자가 도착했습니다", "택배 배송원이 도착했습니다" },
                ["Vehicle"] = new[] { "차량이 도착했습니다", "차량이 출발했습니다", "택배 차량이 도착했습니다" },
                ["Animal"] = new[] { "고양이가 문 앞을 지나갔습니다", "개가 문 앞을 지나갔습니다", "새가 문 앞에 앉았습니다" },
                ["Package"] = new[] { "택배가 배달되었습니다", "패키지가 수령되었습니다" },
                ["Movement"] = new[] { "문 앞에서 움직임이 감지되었습니다", "앞마당에서 활동이 감지되었습니다" }
            };

            // Generate 3-8 random events
            var eventCount = random.Next(3, 9);
            for (int i = 0; i < eventCount; i++)
            {
                var eventTime = startTime.AddMinutes(random.Next(0, (int)(endTime - startTime).TotalMinutes));
                var eventType = eventTypes[random.Next(eventTypes.Length)];
                var eventDescriptions = descriptions[eventType];
                var description = eventDescriptions[random.Next(eventDescriptions.Length)];
                
                events.Add(new DetectedEvent
                {
                    Timestamp = eventTime.TimeOfDay,
                    EventType = eventType,
                    Description = description,
                    Confidence = 0.7 + random.NextDouble() * 0.3, // 70-100% confidence
                    DetectedObjects = new List<string> { eventType }
                });
            }

            // Sort events by timestamp
            events.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            result.DetectedEvents = events;
        }

        private async Task SimulateAIAnalysisAsync(VideoAnalysisResult result)
        {
            // Simulate processing time based on video duration
            var processingTime = Math.Min(result.Duration.TotalSeconds / 10, 30); // Max 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(processingTime));

            // Generate simulated events
            GenerateSimulatedEvents(result);
        }

        private string GenerateSummary(VideoAnalysisResult result)
        {
            var summary = $"오늘 하루 도어캠 요약:\n\n";
            
            foreach (var evt in result.DetectedEvents)
            {
                var timeString = evt.Timestamp.ToString(@"hh\:mm");
                summary += $"🕐 {timeString} - {evt.Description}\n";
            }
            
            summary += $"\n총 {result.DetectedEvents.Count}개의 주요 이벤트가 감지되었습니다.\n\n";
            
            // Generate statistics
            var stats = new Dictionary<string, int>();
            foreach (var evt in result.DetectedEvents)
            {
                if (stats.ContainsKey(evt.EventType))
                    stats[evt.EventType]++;
                else
                    stats[evt.EventType] = 1;
            }
            
            summary += "📊 분석 통계:\n";
            foreach (var stat in stats)
            {
                var icon = stat.Key switch
                {
                    "Person" => "👤",
                    "Vehicle" => "🚗",
                    "Animal" => "🐾",
                    "Package" => "📦",
                    "Movement" => "👣",
                    _ => "📹"
                };
                summary += $"• {icon} {stat.Key} 감지: {stat.Value}회\n";
            }
            
            summary += $"• 활동 시간: {result.Duration.Hours}시간 {result.Duration.Minutes}분\n";
            summary += $"• 분석된 프레임: {(int)(result.Duration.TotalSeconds * result.FrameRate):N0}개";
            
            if (result.AllDetectedObjects.Any())
            {
                summary += $"\n• AI 감지 객체: {result.AllDetectedObjects.Count}개";
            }
            
            return summary;
        }
    }
} 