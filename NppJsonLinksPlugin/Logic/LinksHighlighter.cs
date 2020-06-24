﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using NppJsonLinksPlugin.Configuration;
using NppJsonLinksPlugin.Core;
using NppJsonLinksPlugin.Logic.Context;
using NppJsonLinksPlugin.PluginInfrastructure.Gateway;

namespace NppJsonLinksPlugin.Logic
{
    public class LinksHighlighter : IDisposable
    {
        private const int HIGHLIGHT_INDICATOR_ID = 32;

        private const int STYLE_NONE = 5;
        private const int STYLE_UNDERLINE = 0;
        private const int STYLE_HOVER = 0;

        private readonly IScintillaGateway _gateway;
        private readonly IEnumerable<Settings.MappingItem> _settingsMapping; // if null -> then hightlighting disabled

        private List<Word> _expectedWords;

        private string _currentPath = null;
        private int _startLineIndex = 0;
        private int _endPosition = 0;

        private readonly Timer _updateUiTimer = null;

        private readonly Func<string, int, int, ISearchContext> _searchContextProvider;
        private bool _uiUpdated = true;

        public LinksHighlighter(IScintillaGateway gateway, Settings settings)
        {
            if (!settings.HighlightingEnabled)
            {
                _settingsMapping = null;
                gateway.SetIndicatorStyle(HIGHLIGHT_INDICATOR_ID, STYLE_NONE);
                Logger.Warn("Highlighting is disabled by settings");
                return;
            }

            _settingsMapping = settings.Mapping;
            _gateway = gateway;
            _searchContextProvider = (word, initialLineIndex, indexOfSelectedWord) => new JsonSearchContext(word, gateway, initialLineIndex, indexOfSelectedWord);

            gateway.SetIndicatorStyle(HIGHLIGHT_INDICATOR_ID, STYLE_UNDERLINE);
            _updateUiTimer = CreateTimer(1000);
        }

        public void Dispose()
        {
            _updateUiTimer.Stop();
            _updateUiTimer.Dispose();
            _gateway.SetIndicatorStyle(HIGHLIGHT_INDICATOR_ID, STYLE_NONE);
        }

        private Timer CreateTimer(int interval)
        {
            var timer = new Timer(interval);
            timer.Elapsed += UpdateUi;
            timer.AutoReset = true;
            timer.Start();
            return timer;
        }

        public void MarkUpdated()
        {
            _uiUpdated = true;
        }

        private void UpdateUi(object sender, ElapsedEventArgs e)
        {
            if (_uiUpdated)
            {
                UpdateUi();
                _uiUpdated = false;
            }
        }

        private bool IsHighlightingDisabled()
        {
            return _settingsMapping == null;
        }

        private void UpdateUi()
        {
            if (IsHighlightingDisabled()) return;
            try
            {
                if (Update())
                {
                    HighlightVisibleText();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.Message, e);
            }
        }

        private bool Update()
        {
            var currentPath = StringUtils.NormalizePath(_gateway.GetFullCurrentPath());

            var firstVisibleLine = _gateway.GetFirstVisibleLine(); // здесь не учитваются заколлапшенные строки

            var startLineIndex = _gateway.DocLineFromVisible(firstVisibleLine);
            var linesOnScreen = _gateway.LinesOnScreen();
            var endLineIndex = _gateway.DocLineFromVisible(firstVisibleLine + linesOnScreen);
            var endPosition = _gateway.GetLineEndPosition(endLineIndex).Value;

            bool changed = currentPath != _currentPath;
            if (changed)
            {
                _expectedWords = _settingsMapping
                    .Select(item => item.Src)
                    .Where(src => src.MatchesWithPath(currentPath))
                    .Select(src => src.Word)
                    .ToList();
            }

            changed = changed
                      || startLineIndex != _startLineIndex
                      || endPosition != _endPosition;

            _currentPath = currentPath;
            _startLineIndex = startLineIndex;
            _endPosition = endPosition;

            return changed;
        }

        private void HighlightVisibleText()
        {
            CleanCurrentFileHighlighting();
            if (_expectedWords.Count == 0) return;

            var linesOnScreen = _gateway.LinesOnScreen();
            var totalLinesCount = _gateway.GetLineCount();
            int lineIndex = _startLineIndex - 1;

            while (linesOnScreen-- > 0)
            {
                lineIndex++;
                if (lineIndex >= totalLinesCount) return;

                if (!_gateway.GetLineVisible(lineIndex))
                {
                    // перепрыгиваем hidden/collapsed lines
                    int invisibleNoDocLine = _gateway.VisibleFromDocLine(lineIndex);
                    lineIndex = _gateway.DocLineFromVisible(invisibleNoDocLine);
                }

                var isEscape = false;
                StringBuilder currentWord = null;
                var lineText = _gateway.GetLineText(lineIndex);

                for (int i = 0; i < lineText.Length; i++)
                {
                    var ch = lineText[i];

                    if (!isEscape && ch == '"')
                    {
                        if (currentWord == null)
                        {
                            currentWord = new StringBuilder();
                        }
                        else
                        {
                            string word = currentWord.ToString();
                            var wordLength = word.Length;
                            if (IsNeedToHighlightWord(word, lineIndex, i - wordLength))
                            {
                                int position = _gateway.LineToPosition(lineIndex) + i - wordLength;
                                // int position = _gateway.IndexPositionFromLine(lineIndex, i - wordLength);
                                HighlightWord(position, wordLength);
                            }

                            currentWord = null;
                        }
                    }
                    else if (currentWord != null)
                    {
                        if (ch.IsPartOfWord())
                            currentWord.Append(ch);
                        else
                            currentWord = null;
                    }

                    isEscape = !isEscape && ch == '\\';
                }
            }
        }

        private bool IsNeedToHighlightWord(string word, int lineIndex, int indexOfWord)
        {
            var searchContext = _searchContextProvider.Invoke(word, lineIndex, indexOfWord);
            var contextProperty = searchContext.GetSelectedProperty();

            if (contextProperty == null) return false;

            // нас интересует только подсветка property name, поэтому value - игнорируем
            if (contextProperty.Name != word) return false;

            foreach (var srcWord in _expectedWords)
            {
                if (searchContext.MatchesWith(srcWord, false))
                {
                    return true;
                }
            }

            return false;
        }

        private void HighlightWord(int indexOfWord, int wordLength)
        {
            _gateway.ApplyIndicatorStyleForRange(HIGHLIGHT_INDICATOR_ID, indexOfWord, wordLength);
        }

        private void CleanCurrentFileHighlighting()
        {
            _gateway.ClearIndicatorStyleForRange(HIGHLIGHT_INDICATOR_ID, 0, _gateway.GetTextLength());
        }
    }
}