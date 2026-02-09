 IRealtimeTranscriptionService interface maps perfectly to Azure Speech Services.  
  
- Language -> SpeechConfig.SpeechRecognitionLanguage
- SetAudioFormat(rate, bits, ch) │ AudioStreamFormat.GetWaveFormatPCM(rate, bits, ch)
- Start() ->  SpeechRecognizer.StartContinuousRecognitionAsync()
- Stop() ->   SpeechRecognizer.StopContinuousRecognitionAsync()
- FeedAudio(byte[]) -> PushAudioInputStream.Write(byte[])
- TranscriptionDelta -> Recognizing event (partial results)
- TranscriptionCompleted -> Recognized event (final results)
  
  Azure Speech SDK natively supports 16kHz/44.1kHz/48kHz, so the Azure implementation
  could skip the AudioPreprocessor resampling entirely and just feed raw PCM directly.