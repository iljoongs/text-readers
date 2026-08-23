using System.Windows.Documents;

namespace TextReaders.Services;

public interface ITextToSpeechService
{
    event Action<Paragraph>? ParagraphStarted;

    event Action? PlaybackStopped;

    void Play(IReadOnlyList<Paragraph> paragraphs, int startIndex);

    void Pause();

    void Resume();

    void Stop();
}
