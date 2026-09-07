## 1. Update EmptyHintText strings

- [x] 1.1 `src/lazynats/Objects/BucketListView.cs`: `"No buckets - N to add one, R to refresh"`
- [x] 1.2 `src/lazynats/Values/BucketListView.cs`: `"No buckets - N to add one, R to refresh"`
- [x] 1.3 `src/lazynats/Values/KeyListView.cs`: `"No keys - N to add one, R to refresh"`
- [x] 1.4 `src/lazynats/Objects/ObjectListView.cs`: `"No objects - N to add one, R to refresh"`
- [x] 1.5 `src/lazynats/Streams/StreamListView.cs`: `"No streams - N to add one, R to refresh"`
- [x] 1.6 `src/lazynats/Streams/ConsumerListView.cs`: `"No consumers - N to add one, R to refresh"`
- [x] 1.7 `src/lazynats/Templates/TemplateListView.cs`: `"No templates - N to add one, R to refresh"`

## 2. Verify

- [x] 2.1 `dotnet build src/lazynats.sln` succeeds
- [x] 2.2 Confirm each affected list's empty-state hint renders correctly (e.g. via tmux-driven
      manual check against a running app) for at least one bucket/key/object/stream/consumer view
