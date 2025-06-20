using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Windows.Storage;
using Windows.Graphics.Imaging;
using System.Text;

namespace doorcamPoC.Services
{
    public class AdvancedVideoAnalysisService
    {
        private readonly WindowsAIService _aiService;
        private readonly VideoFrameExtractorService _frameExtractor;
        private readonly VideoAnalysisService _basicAnalysis;

        public AdvancedVideoAnalysisService()
        {
            _aiService = new WindowsAIService();
            _frameExtractor = new VideoFrameExtractorService();
            _basicAnalysis = new VideoAnalysisService();
        }

        public async Task<AdvancedVideoAnalysisResult> AnalyzeVideoAsync(StorageFile videoFile)
        {
            var result = new AdvancedVideoAnalysisResult
            {
                FileName = videoFile.Name,
                FileSize = (long)(await videoFile.GetBasicPropertiesAsync()).Size,
                AnalysisStartTime = DateTime.Now
            };

            try
            {
                // Windows AI 서비스 초기화
                await _aiService.InitializeAsync();

                // 기본 비디오 속성 로드
                await LoadVideoPropertiesAsync(videoFile, result);

                // 고급 프레임 분석
                await PerformAdvancedFrameAnalysisAsync(videoFile, result);

                // 특이점 감지 및 분석
                await DetectAnomaliesAsync(result);

                // 시간대별 패턴 분석
                AnalyzeTimePatterns(result);

                // 상세 요약 생성
                result.DetailedSummary = GenerateDetailedSummary(result);

                result.AnalysisEndTime = DateTime.Now;
                result.AnalysisDuration = result.AnalysisEndTime - result.AnalysisStartTime;

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Advanced video analysis failed: {ex.Message}", ex);
            }
        }

        private async Task LoadVideoPropertiesAsync(StorageFile videoFile, AdvancedVideoAnalysisResult result)
        {
            try
            {
                var videoProps = await videoFile.Properties.GetVideoPropertiesAsync();
                result.Width = videoProps.Width;
                result.Height = videoProps.Height;
                result.Duration = videoProps.Duration;
                result.FrameRate = 30.0; // 기본값
                result.VideoStartTime = videoFile.DateCreated.DateTime;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load video properties: {ex.Message}", ex);
            }
        }

        private async Task PerformAdvancedFrameAnalysisAsync(StorageFile videoFile, AdvancedVideoAnalysisResult result)
        {
            try
            {
                // 프레임 추출 간격을 더 세밀하게 설정
                var interval = result.Duration.TotalMinutes > 60 ? 
                    TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(2);

                var frames = await _frameExtractor.ExtractFramesAsync(videoFile, interval);
                result.TotalFramesAnalyzed = frames.Count;

                // Windows AI를 사용한 프레임별 상세 분석
                var aiEvents = await _aiService.AnalyzeVideoSequenceAsync(frames);
                result.AIEvents = aiEvents;

                // 이벤트 그룹화 및 중요도 계산
                GroupAndPrioritizeEvents(result);
            }
            catch (Exception ex)
            {
                // 고급 분석 실패 시 기본 분석으로 폴백
                var basicResult = await _basicAnalysis.AnalyzeVideoAsync(videoFile);
                ConvertBasicToAdvancedResult(basicResult, result);
            }
        }

        private void GroupAndPrioritizeEvents(AdvancedVideoAnalysisResult result)
        {
            var groupedEvents = result.AIEvents
                .GroupBy(e => new { e.EventType, Hour = e.Timestamp.Hours })
                .OrderBy(g => g.Key.Hour)
                .ThenBy(g => g.First().Timestamp);

            foreach (var group in groupedEvents)
            {
                var firstEvent = group.First();
                var avgConfidence = group.Average(e => e.Confidence);
                var eventCount = group.Count();

                var priorityEvent = new PriorityEvent
                {
                    Timestamp = firstEvent.Timestamp,
                    EventType = firstEvent.EventType,
                    Description = GenerateEnhancedDescription(group.ToList(), result),
                    Confidence = avgConfidence,
                    Frequency = eventCount,
                    Duration = CalculateEventDuration(group.ToList()),
                    Severity = CalculateEventSeverity(firstEvent.EventType, avgConfidence, eventCount)
                };

                result.PriorityEvents.Add(priorityEvent);
            }
        }

        private string GenerateEnhancedDescription(List<VideoEvent> events, AdvancedVideoAnalysisResult result)
        {
            if (events.Count == 1)
            {
                return events[0].Description;
            }
            var firstEvent = events.First();
            var lastEvent = events.Last();
            var duration = lastEvent.Timestamp - firstEvent.Timestamp;
            var timeString = (result.VideoStartTime + firstEvent.Timestamp).ToString("HH:mm:ss");
            return $"{timeString}부터 {duration.TotalMinutes:F1}분간 {events.Count}회의 {GetKoreanEventType(firstEvent.EventType)} 활동이 감지되었습니다. " +
                   $"주요 활동: {firstEvent.Description}";
        }

        private string GetKoreanEventType(string eventType)
        {
            return eventType switch
            {
                "Person" => "인물",
                "Vehicle" => "차량",
                "Animal" => "동물",
                "Package" => "택배",
                "Movement" => "움직임",
                _ => eventType
            };
        }

        private TimeSpan CalculateEventDuration(List<VideoEvent> events)
        {
            if (events.Count <= 1) return TimeSpan.Zero;
            
            var first = events.First().Timestamp;
            var last = events.Last().Timestamp;
            return last - first;
        }

        private EventSeverity CalculateEventSeverity(string eventType, double confidence, int frequency)
        {
            var baseScore = confidence * 10;
            var frequencyScore = Math.Min(frequency * 2, 10);
            var typeScore = eventType switch
            {
                "Person" => 8,
                "Vehicle" => 6,
                "Package" => 5,
                "Animal" => 3,
                "Movement" => 4,
                _ => 5
            };

            var totalScore = (baseScore + frequencyScore + typeScore) / 3;

            return totalScore switch
            {
                >= 8 => EventSeverity.High,
                >= 5 => EventSeverity.Medium,
                _ => EventSeverity.Low
            };
        }

        private async Task DetectAnomaliesAsync(AdvancedVideoAnalysisResult result)
        {
            var anomalies = new List<Anomaly>();

            // 비정상적인 시간대 활동 감지
            var nightEvents = result.AIEvents.Where(e => 
                e.Timestamp.Hours < 6 || e.Timestamp.Hours > 22).ToList();
            
            if (nightEvents.Any())
            {
                anomalies.Add(new Anomaly
                {
                    Type = AnomalyType.NightActivity,
                    Description = $"밤시간({nightEvents.First().Timestamp.Hours}시)에 {nightEvents.Count}개의 활동이 감지되었습니다.",
                    Timestamp = nightEvents.First().Timestamp,
                    Severity = EventSeverity.Medium
                });
            }

            // 반복적인 활동 감지
            var repeatedEvents = result.AIEvents
                .GroupBy(e => e.EventType)
                .Where(g => g.Count() > 3)
                .ToList();

            foreach (var repeated in repeatedEvents)
            {
                anomalies.Add(new Anomaly
                {
                    Type = AnomalyType.RepeatedActivity,
                    Description = $"{GetKoreanEventType(repeated.Key)} 활동이 {repeated.Count()}회 반복되었습니다.",
                    Timestamp = repeated.First().Timestamp,
                    Severity = EventSeverity.Low
                });
            }

            // 긴 지속 시간 활동 감지
            var longDurationEvents = result.PriorityEvents
                .Where(e => e.Duration.TotalMinutes > 10)
                .ToList();

            foreach (var longEvent in longDurationEvents)
            {
                anomalies.Add(new Anomaly
                {
                    Type = AnomalyType.LongDuration,
                    Description = $"{GetKoreanEventType(longEvent.EventType)} 활동이 {longEvent.Duration.TotalMinutes:F1}분간 지속되었습니다.",
                    Timestamp = longEvent.Timestamp,
                    Severity = EventSeverity.Medium
                });
            }

            result.Anomalies = anomalies;
        }

        private void AnalyzeTimePatterns(AdvancedVideoAnalysisResult result)
        {
            var patterns = new List<TimePattern>();

            // 시간대별 활동 패턴 분석
            var hourlyActivity = result.AIEvents
                .GroupBy(e => e.Timestamp.Hours)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var hour in hourlyActivity)
            {
                patterns.Add(new TimePattern
                {
                    Hour = hour.Key,
                    EventCount = hour.Count(),
                    MostCommonEvent = hour.GroupBy(e => e.EventType)
                        .OrderByDescending(g => g.Count())
                        .First().Key,
                    AverageConfidence = hour.Average(e => e.Confidence)
                });
            }

            result.TimePatterns = patterns;
        }

        private string GenerateDetailedSummary(AdvancedVideoAnalysisResult result)
        {
            var summary = new StringBuilder();
            
            summary.AppendLine("🔍 고급 도어캠 분석 결과");
            summary.AppendLine(new string('=', 50));
            summary.AppendLine();

            // 기본 정보
            summary.AppendLine($"📹 비디오 정보:");
            summary.AppendLine($"• 파일명: {result.FileName}");
            summary.AppendLine($"• 길이: {result.Duration.Hours}시간 {result.Duration.Minutes}분");
            summary.AppendLine($"• 해상도: {result.Width}x{result.Height}");
            summary.AppendLine($"• 분석된 프레임: {result.TotalFramesAnalyzed}개");
            summary.AppendLine();

            // 주요 이벤트
            summary.AppendLine("🎯 주요 이벤트:");
            foreach (var priorityEvent in result.PriorityEvents.OrderBy(e => e.Timestamp))
            {
                var severityIcon = priorityEvent.Severity switch
                {
                    EventSeverity.High => "🔴",
                    EventSeverity.Medium => "🟡",
                    EventSeverity.Low => "🟢",
                    _ => "⚪"
                };
                var eventTime = (result.VideoStartTime + priorityEvent.Timestamp).ToString("HH:mm:ss");
                summary.AppendLine($"{severityIcon} {eventTime} - {priorityEvent.Description}");
            }
            summary.AppendLine();

            // 특이점
            if (result.Anomalies.Any())
            {
                summary.AppendLine("⚠️ 특이점 감지:");
                foreach (var anomaly in result.Anomalies)
                {
                    var severityIcon = anomaly.Severity switch
                    {
                        EventSeverity.High => "🔴",
                        EventSeverity.Medium => "🟡",
                        EventSeverity.Low => "🟢",
                        _ => "⚪"
                    };
                    
                    summary.AppendLine($"{severityIcon} {anomaly.Description}");
                }
                summary.AppendLine();
            }

            // 시간대별 패턴
            summary.AppendLine("📊 시간대별 활동 패턴:");
            foreach (var pattern in result.TimePatterns.Where(p => p.EventCount > 0))
            {
                summary.AppendLine($"• {pattern.Hour:D2}시: {pattern.EventCount}개 이벤트 (주요: {GetKoreanEventType(pattern.MostCommonEvent)})");
            }
            summary.AppendLine();

            // 분석 통계
            summary.AppendLine("📈 분석 통계:");
            summary.AppendLine($"• 총 이벤트: {result.AIEvents.Count}개");
            summary.AppendLine($"• 우선순위 이벤트: {result.PriorityEvents.Count}개");
            summary.AppendLine($"• 특이점: {result.Anomalies.Count}개");
            summary.AppendLine($"• 분석 시간: {result.AnalysisDuration.TotalSeconds:F1}초");

            return summary.ToString();
        }

        private void ConvertBasicToAdvancedResult(VideoAnalysisResult basicResult, AdvancedVideoAnalysisResult advancedResult)
        {
            advancedResult.Width = basicResult.Width;
            advancedResult.Height = basicResult.Height;
            advancedResult.Duration = basicResult.Duration;
            advancedResult.FrameRate = basicResult.FrameRate;

            // 기본 결과를 고급 결과로 변환
            foreach (var basicEvent in basicResult.DetectedEvents)
            {
                var aiEvent = new VideoEvent
                {
                    Timestamp = basicEvent.Timestamp,
                    Description = basicEvent.Description,
                    EventType = basicEvent.EventType,
                    Confidence = basicEvent.Confidence,
                    FrameIndex = 0
                };
                advancedResult.AIEvents.Add(aiEvent);
            }

            GroupAndPrioritizeEvents(advancedResult);
        }
    }

    public class AdvancedVideoAnalysisResult
    {
        public string FileName { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public double FrameRate { get; set; }
        public long FileSize { get; set; }
        public int TotalFramesAnalyzed { get; set; }
        public DateTime VideoStartTime { get; set; } = DateTime.Now;
        public List<VideoEvent> AIEvents { get; set; } = new List<VideoEvent>();
        public List<PriorityEvent> PriorityEvents { get; set; } = new List<PriorityEvent>();
        public List<Anomaly> Anomalies { get; set; } = new List<Anomaly>();
        public List<TimePattern> TimePatterns { get; set; } = new List<TimePattern>();
        public string DetailedSummary { get; set; } = string.Empty;
        public DateTime AnalysisStartTime { get; set; }
        public DateTime AnalysisEndTime { get; set; }
        public TimeSpan AnalysisDuration { get; set; }
    }

    public class PriorityEvent
    {
        public TimeSpan Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int Frequency { get; set; }
        public TimeSpan Duration { get; set; }
        public EventSeverity Severity { get; set; }
    }

    public class Anomaly
    {
        public AnomalyType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public TimeSpan Timestamp { get; set; }
        public EventSeverity Severity { get; set; }
    }

    public class TimePattern
    {
        public int Hour { get; set; }
        public int EventCount { get; set; }
        public string MostCommonEvent { get; set; } = string.Empty;
        public double AverageConfidence { get; set; }
    }

    public enum EventSeverity
    {
        Low,
        Medium,
        High
    }

    public enum AnomalyType
    {
        NightActivity,
        RepeatedActivity,
        LongDuration,
        UnusualPattern
    }
} 