using System.Speech.Synthesis;
using System.Windows.Documents;

namespace TextReaders.Services;

// Windows 내장 SAPI(System.Speech)를 사용한다 - 오프라인, API 키 불필요.
// SpeakAsync는 한 번에 한 문장만 재생하므로, 문단을 큐로 미리 넣지 않고
// SpeakCompleted에서 다음 문단을 직접 이어 말하게 해 "지금 어느 문단을 읽는지"를 항상 알 수 있게 한다.
public sealed class TextToSpeechService : ITextToSpeechService
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private IReadOnlyList<Paragraph> _paragraphs = Array.Empty<Paragraph>();
    private int _currentIndex = -1;

    public event Action<Paragraph>? ParagraphStarted;

    public event Action? PlaybackStopped;

    public TextToSpeechService()
    {
        _synthesizer.SpeakCompleted += OnSpeakCompleted;
    }

    public void Play(IReadOnlyList<Paragraph> paragraphs, int startIndex)
    {
        _synthesizer.SpeakAsyncCancelAll();
        _paragraphs = paragraphs;
        _currentIndex = startIndex;
        SpeakCurrent();
    }

    public void Pause()
    {
        if (_synthesizer.State == SynthesizerState.Speaking)
        {
            _synthesizer.Pause();
        }
    }

    public void Resume()
    {
        if (_synthesizer.State == SynthesizerState.Paused)
        {
            _synthesizer.Resume();
        }
    }

    public void Stop()
    {
        _currentIndex = -1;
        _synthesizer.SpeakAsyncCancelAll();
    }

    private void SpeakCurrent()
    {
        if (_currentIndex < 0 || _currentIndex >= _paragraphs.Count)
        {
            _currentIndex = -1;
            PlaybackStopped?.Invoke();
            return;
        }

        var paragraph = _paragraphs[_currentIndex];
        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();
        if (text.Length == 0)
        {
            _currentIndex++;
            SpeakCurrent();
            return;
        }

        ParagraphStarted?.Invoke(paragraph);
        _synthesizer.SpeakAsync(text);
    }

    private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
    {
        if (e.Cancelled || _currentIndex < 0)
        {
            return;
        }

        _currentIndex++;
        SpeakCurrent();
    }
}
