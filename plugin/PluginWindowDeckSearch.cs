﻿using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFTriadBuddy;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public unsafe class PluginWindowDeckSearch : Window, IDisposable
    {
        private const float WindowContentWidth = 270.0f;

        private readonly UIReaderTriadDeckEdit uiReaderDeckEdit;
        private readonly Configuration config;

        private List<Tuple<TriadCard, GameCardInfo>> listCards = new();

        private int selectedCardIdx;
        private ImGuiTextFilterPtr searchFilter;

        private int prevNumFiltered;
        private int prevNumCards;

        public PluginWindowDeckSearch(UIReaderTriadDeckEdit uiReaderDeckEdit, Configuration config) : base("Deck Search")
        {
            this.uiReaderDeckEdit = uiReaderDeckEdit;
            this.config = config;

            var searchFilterPtr = ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null);
            searchFilter = new ImGuiTextFilterPtr(searchFilterPtr);

            uiReaderDeckEdit.OnVisibilityChanged += (_) => UpdateWindowData();
            UpdateWindowData();

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.Always;

            SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(WindowContentWidth + 20, 0), MaximumSize = new Vector2(WindowContentWidth + 20, 1000) };

            ForceMainWindow = true;
            RespectCloseHotkey = false;
            Flags = ImGuiWindowFlags.NoDecoration |
                //ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                //ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;
        }

        public void Dispose()
        {
            ImGuiNative.ImGuiTextFilter_destroy(searchFilter.NativePtr);
        }

        private void UpdateWindowData()
        {
            bool wasOpen = IsOpen;
            IsOpen = uiReaderDeckEdit.IsVisible;

            if (IsOpen && !wasOpen)
            {
                GameCardDB.Get().Refresh();
                searchFilter.Clear();

                GenerateCardList();
            }
        }

        public void GenerateCardList()
        {
            var cardDB = TriadCardDB.Get();
            var cardInfoDB = GameCardDB.Get();

            listCards.Clear();
            foreach (var card in cardDB.cards)
            {
                if (card != null && card.IsValid())
                {
                    var cardInfo = cardInfoDB.FindById(card.Id);
                    if (cardInfo != null && cardInfo.IsOwned)
                    {
                        listCards.Add(new Tuple<TriadCard, GameCardInfo>(card, cardInfo));
                    }
                }
            }

            if (listCards.Count > 1)
            {
                listCards.Sort((a, b) => a.Item1.Name.GetLocalized().CompareTo(b.Item1.Name.GetLocalized()));
            }

            selectedCardIdx = -1;
        }

        public override void PreDraw()
        {
            Position = new Vector2(uiReaderDeckEdit.cachedState.screenPos.X + uiReaderDeckEdit.cachedState.screenSize.X + 10, uiReaderDeckEdit.cachedState.screenPos.Y);
        }

        public override void Draw()
        {
            searchFilter.Draw("", WindowContentWidth * ImGuiHelpers.GlobalScale);

            var filteredCards = new List<int>();
            if (ImGui.BeginListBox("##cards", new Vector2(WindowContentWidth * ImGuiHelpers.GlobalScale, ImGui.GetTextLineHeightWithSpacing() * 10)))
            {
                for (int idx = 0; idx < listCards.Count; idx++)
                {
                    var (cardOb, cardInfo) = listCards[idx];

                    var itemDesc = cardOb.Name.GetLocalized();
                    if (searchFilter.PassFilter(itemDesc))
                    {
                        bool isSelected = selectedCardIdx == idx;
                        if (ImGui.Selectable($"{(int)cardOb.Rarity + 1}★   {itemDesc}", isSelected))
                        {
                            selectedCardIdx = idx;
                            OnCardSelectionChanged();
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }

                        filteredCards.Add(cardOb.Id);
                    }
                }
                ImGui.EndListBox();
            }

            bool hasFilteredCardsChanges = (prevNumCards != listCards.Count) || (prevNumFiltered != filteredCards.Count);
            bool hasSomeCardsFiltered = (filteredCards.Count > 0) && (filteredCards.Count != listCards.Count);
            if (hasFilteredCardsChanges && hasSomeCardsFiltered && config.ShowDeckEditHighlights)
            {
                prevNumCards = listCards.Count;
                prevNumFiltered = filteredCards.Count;

                uiReaderDeckEdit.SetSearchResultHighlight(filteredCards.ToArray());
            }
        }

        private void OnCardSelectionChanged()
        {
            var (cardOb, cardInfo) = (selectedCardIdx >= 0) && (selectedCardIdx < listCards.Count) ? listCards[selectedCardIdx] : null;
            if (cardOb != null && cardInfo != null)
            {
                var collectionPos = cardInfo.Collection[(int)GameCardCollectionFilter.DeckEditDefault];

                //Dalamud.Logging.Service.logger.Info($"Card selection! {cardOb.Name.GetLocalized()} => page:{collectionPos.PageIndex}, cell:{collectionPos.CellIndex}");
                uiReaderDeckEdit.SetPageAndGridView(collectionPos.PageIndex, collectionPos.CellIndex);

                if (config.ShowDeckEditHighlights)
                {
                    uiReaderDeckEdit.SetSearchResultHighlight(new int[] { cardOb.Id });
                }
            }
        }
    }
}
