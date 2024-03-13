﻿using System.ComponentModel;

namespace STranslate.Model
{
    public interface ITranslatorAI : ITranslator
    {
        BindingList<Prompt> Prompts { get; set; }

        BindingList<UserDefinePrompt> UserDefinePrompts { get; set; }
    }
}