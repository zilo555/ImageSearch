using System.Collections;
using Masuit.Tools;
using Masuit.Tools.Media;
using SkiaSharp;
using System.Collections.Concurrent;
using System.IO;
using 以图搜图.Models;

namespace 以图搜图.Services;

public class ImageSearchService
{
    public async Task<List<SearchResult>> SearchAsync(string filename, ConcurrentDictionary<string, IndexItem> index, ConcurrentDictionary<string, FrameIndexItem> frameIndex, MatchAlgorithm algorithm, float similarity, bool checkRotated, bool checkFlipped)
    {
        var parallelism = Environment.ProcessorCount * 4;
        return await Task.Run(() =>
        {
            var defHashs = new ConcurrentBag<ulong[]>();
            var dctHashs = new ConcurrentBag<ulong>();
            var pHashs = new ConcurrentBag<ulong>();
            var actions = new List<Action>();

            if (filename.EndsWith("gif", StringComparison.OrdinalIgnoreCase))
            {
                using var frames = new DisposeCollection<SKBitmap>(SkiaImageHelper.DecodeGrayFrames(filename,160));
                foreach (var frame in frames.Items)
                {
                    actions.Add(() =>
                    {
                            if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                            {
                                defHashs.Add(frame.DifferenceHash256());
                            }

                            if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                            {
                                dctHashs.Add(frame.DctHash());
                            }
                            if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                            {
                                pHashs.Add(frame.DctHash64());
                            }
                            frame.Dispose();
                    });
                }

                Parallel.Invoke(actions.ToArray());
            }
            else
            {
                using (var image = SkiaImageHelper.DecodeGrayThumb(filename,160))
                {
                    if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                    {
                        actions.Add(() => defHashs.Add(image.DifferenceHash256()));
                    }

                    if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                    {
                        actions.Add(() => dctHashs.Add(image.DctHash()));
                    }
                    if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                    {
                        actions.Add(() => pHashs.Add(image.DctHash64()));
                    }
                    if (checkRotated)
                    {
                        actions.Add(() =>
                        {
                            using var clone = image.Rotate(90);
                            if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                            {
                                defHashs.Add(clone.DifferenceHash256());
                            }

                            if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                            {
                                dctHashs.Add(clone.DctHash());
                            }
                            if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                            {
                                pHashs.Add(clone.DctHash64());
                            }
                        });
                        actions.Add(() =>
                        {
                            using var clone = image.Rotate(180);
                            if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                            {
                                defHashs.Add(clone.DifferenceHash256());
                            }

                            if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                            {
                                dctHashs.Add(clone.DctHash());
                            }
                            if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                            {
                                pHashs.Add(clone.DctHash64());
                            }
                        });
                        actions.Add(() =>
                        {
                            using var clone = image.Rotate(270);
                            if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                            {
                                defHashs.Add(clone.DifferenceHash256());
                            }

                            if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                            {
                                dctHashs.Add(clone.DctHash());
                            }
                            if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                            {
                                pHashs.Add(clone.DctHash64());
                            }
                        });
                    }

                    if (checkFlipped)
                    {
                        actions.Add(() =>
                        {
                            using var clone = image.FlipHorizontal();
                            if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                            {
                                defHashs.Add(clone.DifferenceHash256());
                            }

                            if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                            {
                                dctHashs.Add(clone.DctHash());
                            }
                            if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                            {
                                pHashs.Add(clone.DctHash64());
                            }
                        });
                        actions.Add(() =>
                        {
                            using var clone = image.FlipVertical();
                            if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                            {
                                defHashs.Add(clone.DifferenceHash256());
                            }

                            if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                            {
                                dctHashs.Add(clone.DctHash());
                            }
                            if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                            {
                                pHashs.Add(clone.DctHash64());
                            }
                        });
                    }

                    Parallel.Invoke(actions.ToArray());
                }
            }

            var list = new List<SearchResult>();

            if (filename.EndsWith("gif", StringComparison.OrdinalIgnoreCase))
            {
                list.AddRange(frameIndex.AsParallel().WithDegreeOfParallelism(parallelism).SelectMany(x =>
                {
                    var items = new List<SearchResult>(4);
                    if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                    {
                        items.Add(new SearchResult
                        {
                            路径 = x.Key,
                            匹配度 = x.Value.DifferenceHash.SelectMany(h => defHashs.Select(hh => ImageHasher.Compare(h, hh)).Where(f => f >= similarity)).OrderDescending().Take(10).DefaultIfEmpty().Average(),
                            匹配算法 = "Difference Hash"
                        });
                    }
                    var sim = Math.Max(0.85, similarity);
                    if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                    {
                        items.Add(new SearchResult
                        {
                            路径 = x.Key,
                            匹配度 = x.Value.DctHash64.SelectMany(h => pHashs.Select(hh => ImageHasher.Compare(h, hh)).Where(f => f >= sim)).OrderDescending().Take(10).DefaultIfEmpty().Average(),
                            匹配算法 = "DCT Hash 64"
                        });
                    }
                    if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                    {
                        items.Add(new SearchResult
                        {
                            路径 = x.Key,
                            匹配度 = x.Value.DctHash.SelectMany(h => dctHashs.Select(hh => ImageHasher.Compare(h, hh)).Where(f => f >= sim)).OrderDescending().Take(10).DefaultIfEmpty().Average(),
                            匹配算法 = "DCT Hash 32"
                        });
                    }
                    return items;
                }).Where(x => x.匹配度 >= similarity));
            }
            else
            {
                var sim = Math.Max(0.85, similarity);
                list.AddRange(frameIndex.AsParallel().WithDegreeOfParallelism(parallelism).SelectMany(x =>
                {
                    var items = new List<SearchResult>(4);
                    if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                    {
                        items.Add(new SearchResult
                        {
                            路径 = x.Key,
                            匹配度 = x.Value.DctHash64.SelectMany(h => pHashs.Select(hh => ImageHasher.Compare(h, hh)).Where(f => f >= sim)).MaxOrDefault(),
                            匹配算法 = "DCT Hash 64"
                        });
                    }
                    if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                    {
                        items.Add(new SearchResult
                        {
                            路径 = x.Key,
                            匹配度 = x.Value.DifferenceHash.SelectMany(h => defHashs.Select(hh => ImageHasher.Compare(h, hh)).Where(f => f >= similarity)).MaxOrDefault(),
                            匹配算法 = "Difference Hash"
                        });
                    }
                    if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                    {
                        items.Add(new SearchResult
                        {
                            路径 = x.Key,
                            匹配度 = x.Value.DctHash.SelectMany(h => dctHashs.Select(hh => ImageHasher.Compare(h, hh)).Where(f => f >= sim)).MaxOrDefault(),
                            匹配算法 = "DCT Hash 32"
                        });
                    }
                    return items;
                }).Where(x => x.匹配度 >= similarity));

                list.AddRange(index.Chunk(parallelism).AsParallel().WithDegreeOfParallelism(parallelism).SelectMany(grouping =>
                {
                    var items = new List<SearchResult>();
                    foreach (var (key, value) in grouping)
                    {
                        if (algorithm.HasFlag(MatchAlgorithm.DctHash64))
                        {
                            var match = pHashs.Max(h => ImageHasher.Compare(value.DctHash64, h));
                            if (match > sim)
                            {
                                items.Add(new SearchResult
                                {
                                    路径 = key,
                                    匹配度 = match,
                                    匹配算法 = "DCT Hash 64"
                                });
                            }
                        }
                        if (algorithm.HasFlag(MatchAlgorithm.DifferenceHash))
                        {
                            var match = defHashs.Max(h => ImageHasher.Compare(value.DifferenceHash, h));
                            if (match > similarity)
                            {
                                items.Add(new SearchResult
                                {
                                    路径 = key,
                                    匹配度 = match,
                                    匹配算法 = "Difference Hash"
                                });
                            }
                        }
                        if (algorithm.HasFlag(MatchAlgorithm.DctHash32))
                        {
                            var match = dctHashs.Max(h => ImageHasher.Compare(value.DctHash, h));
                            if (match > sim)
                            {
                                items.Add(new SearchResult
                                {
                                    路径 = key,
                                    匹配度 = match,
                                    匹配算法 = "DCT Hash 32"
                                });
                            }
                        }
                    }
                    return items;
                }));
            }

            list = list.OrderByDescending(a => a.匹配度).DistinctBy(e => e.路径).ToList();
            var dic = list.Where(e => File.Exists(e.路径)).GroupBy(r => new FileInfo(r.路径).DirectoryName).Where(g => g.Key != null).AsParallel().WithDegreeOfParallelism(parallelism).Select(g =>
            {
                var files = new DirectoryInfo(g.Key!).GetFiles("*.*", SearchOption.AllDirectories);
                return new
                {
                    Key = g.Key!,
                    files.Length,
                    Size = files.Sum(s => s.Length) / 1048576f
                };
            }).ToDictionary(a => a.Key);

            list.Where(e => File.Exists(e.路径)).OrderBy(e => e.路径).ForEach(result =>
            {
                var file = new FileInfo(result.路径);
                result.大小 = $"{file.Length / 1024}KB";
                var dirName = file.DirectoryName!;
                if (dic.ContainsKey(dirName))
                {
                    result.所属文件夹文件数 = dic[dirName].Length;
                    result.所属文件夹大小 = $"{dic[dirName].Size:F2}MB";
                }
            });

            return list;
        });
    }
}

internal sealed class DisposeCollection<T>(IEnumerable<T> items) :IEnumerable<T>, IDisposable where T : IDisposable
{
    public List<T> Items { get; } = items.ToList();

    public void Dispose()
    {
        foreach (var item in Items)
        {
            item.Dispose();
        }
    }

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}