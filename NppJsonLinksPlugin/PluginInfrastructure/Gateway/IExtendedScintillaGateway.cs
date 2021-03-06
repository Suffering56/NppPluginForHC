﻿using NppJsonLinksPlugin.Logic;

namespace NppJsonLinksPlugin.PluginInfrastructure.Gateway
{
    public partial interface IScintillaGateway
    {
        void OpenFile(string filePath);

        string GetFullCurrentPath();

        int PositionToLine(int position);

        int LineToPosition(int line);

        string GetCurrentWord();

        int GetCurrentLine();

        void JumpToLine(int line);

        JumpLocation GetCurrentLocation();

        void SetIndicatorStyle(int indicatorId, int indicatorStyle);

        void ApplyIndicatorStyleForRange(int indicatorId, int startPosition, int length);

        void ClearIndicatorStyleForRange(int indicatorId, int startPosition, int length);
        
        int IndexPositionFromLine(int lineIndex, int linePosition);
    }
}