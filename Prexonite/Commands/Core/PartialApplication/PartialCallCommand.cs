﻿namespace Prexonite.Commands.Core.PartialApplication
{
    /// <summary>
    /// <para>Used to implement partial application for indirect calls (the default call interface in Prexonite)</para>
    /// </summary>
    public class PartialCallCommand : PartialApplicationCommandBase
    {

        #region Overrides of PartialApplicationCommandBase

        protected override IIndirectCall CreatePartialApplication(sbyte[] mappings, PValue[] closedArguments)
        {
            return new PartialCall(mappings, closedArguments);
        }

        #endregion
    }
}