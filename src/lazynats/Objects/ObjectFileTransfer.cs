namespace lazynats.Objects;

// ObjectFileDialog's own result type - mirrors NewKeyOptions' shape. Key is the object's name
// (fixed to the highlighted object in download mode); Path is the local file path.
internal sealed record ObjectFileTransfer(string Key, string Path);
