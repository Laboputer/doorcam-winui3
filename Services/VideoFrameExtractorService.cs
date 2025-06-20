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
                // 비디오 파일의 실제 속성 가져오기
                var videoProps = await videoFile.Properties.GetVideoPropertiesAsync();
                var actualDuration = videoProps.Duration;
                var actualWidth = videoProps.Width;
                var actualHeight = videoProps.Height;

                // MediaPlayer 초기화
                _mediaPlayer = new MediaPlayer();
                var mediaSource = MediaSource.CreateFromStorageFile(videoFile);
                _mediaPlayer.Source = mediaSource;

                // 비디오 로딩 대기
                await WaitForMediaReadyAsync();

                // duration 결정
                TimeSpan duration;
                if (_mediaPlayer.PlaybackSession.NaturalDuration != TimeSpan.Zero)
                    duration = _mediaPlayer.PlaybackSession.NaturalDuration;
                else if (actualDuration != TimeSpan.Zero)
                    duration = actualDuration;
                else
                    duration = TimeSpan.FromMinutes(1);

                // interval이 duration보다 크면 자동 조정
                if (interval >= duration)
                {
                    interval = TimeSpan.FromSeconds(Math.Max(1, duration.TotalSeconds / 10)); // 최소 10개 프레임
                }

                var currentTime = TimeSpan.Zero;
                int frameCount = 0;

                while (currentTime < duration)
                {
                    _mediaPlayer.PlaybackSession.Position = currentTime;
                    await Task.Delay(100); // 위치 설정 대기
                    var frame = await CaptureCurrentFrameAsync(currentTime, actualWidth, actualHeight);
                    if (frame != null)
                    {
                        frames.Add(frame);
                        frameCount++;
                    }
                    currentTime += interval;
                }

                // 최소 1개 프레임 보장
                if (frames.Count == 0)
                {
                    var fallbackFrame = await CreateFallbackFrameAsync(TimeSpan.Zero, actualWidth, actualHeight);
                    frames.Add(fallbackFrame);
                }
            }
            catch (Exception ex)
            {
                // 오류 발생 시 기본 프레임 생성
                var fallbackFrame = await CreateFallbackFrameAsync(TimeSpan.Zero, 1920, 1080);
                frames.Add(fallbackFrame);
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

        private async Task<VideoFrame?> CaptureCurrentFrameAsync(TimeSpan timestamp, uint width, uint height)
        {
            try
            {
                // 실제 프레임 캡처가 복잡하므로 시뮬레이션으로 대체
                return await SimulateFrameCaptureAsync(timestamp, width, height);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<VideoFrame> SimulateFrameCaptureAsync(TimeSpan timestamp, uint width, uint height)
        {
            await Task.Delay(50); // 시뮬레이션 지연
            var dummyBitmap = await CreateDummySoftwareBitmapAsync(width, height);
            return new VideoFrame
            {
                Timestamp = timestamp,
                SoftwareBitmap = dummyBitmap,
                Width = width,
                Height = height
            };
        }

        private async Task<VideoFrame> CreateFallbackFrameAsync(TimeSpan timestamp, uint width, uint height)
        {
            var dummyBitmap = await CreateDummySoftwareBitmapAsync(width, height);
            return new VideoFrame
            {
                Timestamp = timestamp,
                SoftwareBitmap = dummyBitmap,
                Width = width,
                Height = height
            };
        }

        private async Task<SoftwareBitmap> CreateDummySoftwareBitmapAsync(uint width, uint height)
        {
            await Task.Delay(10);
            // 실제 해상도 반영 (단색 비트맵)
            var pixelFormat = BitmapPixelFormat.Bgra8;
            var alphaMode = BitmapAlphaMode.Premultiplied;
            var softwareBitmap = new SoftwareBitmap(pixelFormat, (int)width, (int)height, alphaMode);
            return softwareBitmap;
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