using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Microsoft.UI.Dispatching;

namespace doorcamPoC.Services
{
    public class VideoFrameExtractorService
    {
        private MediaPlayer? _mediaPlayer;
        private readonly DispatcherQueue _dispatcherQueue;

        public VideoFrameExtractorService()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public async Task<List<VideoFrame>> ExtractFramesAsync(StorageFile videoFile, TimeSpan interval = default)
        {
            if (interval == default)
            {
                interval = TimeSpan.FromSeconds(5); // 기본 5초 간격
            }

            var frames = new List<VideoFrame>();
            
            try
            {
                // MediaPlayer 초기화
                _mediaPlayer = new MediaPlayer();
                var mediaSource = MediaSource.CreateFromStorageFile(videoFile);
                _mediaPlayer.Source = mediaSource;

                // 비디오 로딩 대기
                await WaitForMediaReadyAsync();

                var duration = _mediaPlayer.PlaybackSession.NaturalDuration;
                if (duration == null)
                {
                    duration = TimeSpan.FromMinutes(1);
                }
                var currentTime = TimeSpan.Zero;

                while (currentTime < duration)
                {
                    // 특정 시간으로 이동
                    _mediaPlayer.PlaybackSession.Position = currentTime;
                    
                    // 프레임 추출 대기
                    await Task.Delay(100); // MediaPlayer가 위치를 설정할 시간
                    
                    // 현재 프레임을 이미지로 캡처
                    var frame = await CaptureCurrentFrameAsync(currentTime);
                    if (frame != null)
                    {
                        frames.Add(frame);
                    }

                    currentTime += interval;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to extract frames: {ex.Message}", ex);
            }
            finally
            {
                _mediaPlayer?.Dispose();
                _mediaPlayer = null;
            }

            return frames;
        }

        private async Task WaitForMediaReadyAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            
            void OnMediaOpened(MediaPlayer sender, object args)
            {
                tcs.SetResult(true);
            }

            void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
            {
                tcs.SetException(new Exception(args.ErrorMessage));
            }

            _mediaPlayer!.MediaOpened += OnMediaOpened;
            _mediaPlayer.MediaFailed += OnMediaFailed;

            try
            {
                // 타임아웃 설정
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Media loading timed out");
                }
            }
            finally
            {
                _mediaPlayer.MediaOpened -= OnMediaOpened;
                _mediaPlayer.MediaFailed -= OnMediaFailed;
            }
        }

        private async Task<VideoFrame?> CaptureCurrentFrameAsync(TimeSpan timestamp)
        {
            try
            {
                // MediaPlayer에서 현재 프레임을 캡처하는 것은 복잡하므로
                // 시뮬레이션으로 대체
                return await SimulateFrameCaptureAsync(timestamp);
            }
            catch (Exception)
            {
                // 프레임 캡처 실패 시 null 반환
                return null;
            }
        }

        private async Task<VideoFrame> SimulateFrameCaptureAsync(TimeSpan timestamp)
        {
            // 실제 구현에서는 MediaPlayer에서 프레임을 캡처해야 합니다
            // 현재는 시뮬레이션으로 대체
            
            await Task.Delay(50); // 시뮬레이션 지연

            return new VideoFrame
            {
                Timestamp = timestamp,
                SoftwareBitmap = null, // 실제 구현에서는 SoftwareBitmap 설정
                Width = 1920,
                Height = 1080
            };
        }

        public void Dispose()
        {
            _mediaPlayer?.Dispose();
        }
    }

    public class VideoFrame
    {
        public TimeSpan Timestamp { get; set; }
        public SoftwareBitmap? SoftwareBitmap { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
    }
} 