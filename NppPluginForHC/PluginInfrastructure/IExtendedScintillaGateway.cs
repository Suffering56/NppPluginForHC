﻿namespace NppPluginForHC.PluginInfrastructure
{
    public interface IExtendedScintillaGateway : IScintillaGateway
    {
        int PositionToLine(int position);

        int LineToPosition(int line);

        string GetTextFromPosition(int startPosition, int length);
    }
}