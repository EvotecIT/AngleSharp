namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class SelectMutationTests
    {
        [Test]
        public void MovingAndRemovingAGroupNormalizesBothSelects()
        {
            var document = ("<select id=source><option value=a>A</option><optgroup><option value=b selected>B</option></optgroup></select>" +
                "<select id=target><option value=c>C</option></select>").ToHtmlDocument();
            var source = document.QuerySelector<IHtmlSelectElement>("#source");
            var target = document.QuerySelector<IHtmlSelectElement>("#target");
            var group = document.QuerySelector<IHtmlOptionsGroupElement>("optgroup");

            target.InsertBefore(group, target.FirstChild);
            Assert.AreEqual("a", source.Value);
            Assert.AreEqual("b", target.Value);
            Assert.IsFalse(target.Options[1].IsSelected);

            target.RemoveChild(group);
            Assert.AreEqual("c", target.Value);
            group.InnerHtml = "<option value=d>D</option>";
            source.AddOption(group);
            source.RemoveOptionAt(0);
            Assert.AreEqual("d", source.Value);
        }

        [TestCase("remove")]
        [TestCase("replace")]
        public void CollectionMutationUsesTheGroupedOptionsParent(String route)
        {
            var document = "<select><optgroup><option value=a>A</option><option value=b>B</option></optgroup></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");

            if (route == "remove")
            {
                select.RemoveOptionAt(0);
                Assert.AreEqual("b", select.Value);
                Assert.AreEqual(1, select.Length);
            }
            else
            {
                var replacement = (IHtmlOptionElement)document.CreateElement("option");
                replacement.Value = "c";
                replacement.IsSelected = true;
                select[0] = replacement;
                Assert.AreEqual("c", select.Value);
                Assert.AreEqual("optgroup", replacement.ParentElement.LocalName);
            }
        }

        [Test]
        public void AddingBeforeAGroupedOptionUsesTheReferenceParent()
        {
            var document = "<select><optgroup><option value=a>A</option></optgroup></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            var reference = select.Options[0];
            var option = (IHtmlOptionElement)document.CreateElement("option");
            option.Value = "b";
            option.IsSelected = true;

            select.AddOption(option, reference);

            Assert.AreSame(reference.Parent, option.Parent);
            Assert.AreEqual("b", select.Value);
            Assert.AreEqual(0, select.SelectedIndex);
        }

        [Test]
        public void AddingAGroupBeforeAGroupedOptionUsesTheReferenceParent()
        {
            var document = "<select><optgroup><option value=a>A</option></optgroup></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            var reference = select.Options[0];
            var group = (IHtmlOptionsGroupElement)document.CreateElement("optgroup");

            select.AddOption(group, reference);

            Assert.AreSame(reference.Parent, group.Parent);
            Assert.AreSame(reference, group.NextSibling);
            Assert.AreEqual("a", select.Value);
            Assert.AreEqual(1, select.Length);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SelectedInsertionBeforeAPeerWinsWithoutDirtyingThePeer(Boolean fragment, Boolean firstSelected)
        {
            var document = "<select size=2><option value=peer selected>Peer</option></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            var peer = select.Options[0];
            var selected = (IHtmlOptionElement)document.CreateElement("option");
            selected.Value = "new";
            selected.IsDefaultSelected = true;

            if (fragment)
            {
                var nodes = document.CreateDocumentFragment();
                var first = (IHtmlOptionElement)document.CreateElement("option");
                first.IsDefaultSelected = firstSelected;
                nodes.AppendChild(first);
                nodes.AppendChild(selected);
                select.InsertBefore(nodes, peer);
            }
            else
            {
                select.InsertBefore(selected, peer);
            }

            Assert.AreEqual("new", select.Value);
            Assert.IsFalse(peer.IsSelected);
            peer.IsDefaultSelected = false;
            peer.IsDefaultSelected = true;
            Assert.AreEqual("peer", select.Value);
            Assert.IsFalse(selected.IsSelected);
        }

        [Test]
        public void MovingASelectedOptionIntoAGroupNormalizesBothOwners()
        {
            var document = ("<select id=source><option value=a selected>A</option><option value=b>B</option></select>" +
                "<select id=target><optgroup><option value=c>C</option></optgroup></select>").ToHtmlDocument();
            var source = document.QuerySelector<IHtmlSelectElement>("#source");
            var target = document.QuerySelector<IHtmlSelectElement>("#target");
            var option = source.Options[0];
            document.QuerySelector("optgroup").AppendChild(option);

            Assert.AreEqual("b", source.Value);
            Assert.AreEqual("a", target.Value);
            Assert.IsFalse(target.Options[0].IsSelected);
            option.IsDefaultSelected = false;
            Assert.AreEqual("c", target.Value);
        }

        [Test]
        public void ClearingValueRemainsObservableUntilASelectionResettingMutation()
        {
            var document = "<form action='https://example.com/'><select name=choice><option value=a>A</option></select></form>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            select.Value = "missing";
            select.SetAttribute("title", "unchanged selection");

            Assert.IsNull(select.Value);
            Assert.AreEqual(-1, select.SelectedIndex);
            Assert.AreEqual(0, select.SelectedOptions.Length);
            Assert.AreEqual(String.Empty, document.Forms[0].GetSubmission().Target.Query);

            select.AppendChild(document.CreateElement("option"));
            Assert.AreEqual("a", select.Value);
            Assert.AreEqual(0, select.SelectedIndex);
        }

        [Test]
        public void SizeAndMultipleChangesNormalizeWithoutDirtyingCleanOptions()
        {
            var document = "<select size=2><option value=a>A</option><option value=b>B</option></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            Assert.AreEqual(-1, select.SelectedIndex);
            select.Size = 1;
            Assert.AreEqual("a", select.Value);

            select.IsMultiple = true;
            select.Options[1].IsDefaultSelected = true;
            Assert.AreEqual(2, select.SelectedOptions.Length);
            select.IsMultiple = false;
            Assert.AreEqual("a", select.Value);
            Assert.AreEqual(1, select.SelectedOptions.Length);

            select.Options[1].IsDefaultSelected = false;
            select.Options[1].IsDefaultSelected = true;
            Assert.AreEqual("b", select.Value);
        }

        [Test]
        public void GroupInsertionResetsSelectionAfterDisabledChangesPreserveIt()
        {
            var document = "<select><optgroup disabled><option value=a disabled>A</option></optgroup></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            var group = document.QuerySelector<IHtmlOptionsGroupElement>("optgroup");
            Assert.AreEqual(-1, select.SelectedIndex);

            select.Options[0].IsDisabled = false;
            Assert.AreEqual(-1, select.SelectedIndex);
            group.IsDisabled = false;
            Assert.AreEqual(-1, select.SelectedIndex);

            var option = (IHtmlOptionElement)document.CreateElement("option");
            option.Value = "b";
            group.AppendChild(option);
            Assert.AreEqual("a", select.Value);

            group.InnerHtml = "<option value=c>C</option>";
            Assert.AreEqual("c", select.Value);
        }

        [TestCase("value", false)]
        [TestCase("index", true)]
        public void DisabledChangesPreserveAnExplicitlyClearedSelection(String route, Boolean disableGroup)
        {
            var document = "<select><optgroup><option value=a>A</option><option value=b>B</option></optgroup></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");

            if (route == "value")
            {
                select.Value = "missing";
            }
            else
            {
                select.Options.SelectedIndex = -1;
            }

            if (disableGroup)
            {
                var group = document.QuerySelector<IHtmlOptionsGroupElement>("optgroup");
                group.IsDisabled = true;
                group.IsDisabled = false;
            }
            else
            {
                select.Options[0].IsDisabled = true;
                select.Options[0].IsDisabled = false;
            }

            Assert.AreEqual(-1, select.SelectedIndex);
            Assert.IsNull(select.Value);
            Assert.AreEqual(0, select.SelectedOptions.Length);
        }

        [Test]
        public void EnablingAnOptionDoesNotChooseASelection()
        {
            var document = "<select><option value=a disabled>A</option><option value=b disabled>B</option></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            Assert.AreEqual(-1, select.SelectedIndex);

            select.Options[0].IsDisabled = false;

            Assert.AreEqual(-1, select.SelectedIndex);
            Assert.IsNull(select.Value);
            Assert.AreEqual(0, select.SelectedOptions.Length);
        }

        [TestCase("value")]
        [TestCase("index")]
        public void SelectingThroughTheCollectionOrValueDirtiesOnlyTheTarget(String route)
        {
            var document = "<select size=2><option value=a selected>A</option><option value=b>B</option></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");

            if (route == "value")
            {
                select.Value = "b";
            }
            else
            {
                select.Options.SelectedIndex = 1;
            }

            select.Options[0].IsDefaultSelected = false;
            select.Options[0].IsDefaultSelected = true;
            Assert.AreEqual("a", select.Value);
            select.Options[1].IsDefaultSelected = true;
            Assert.IsFalse(select.Options[1].IsSelected);
        }

        [TestCase(-1)]
        [TestCase(10)]
        public void ClearingSelectedIndexDoesNotDirtyTheUnselectedOptions(Int32 index)
        {
            var document = "<select size=2><option selected>A</option><option>B</option></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            select.Options.SelectedIndex = index;
            Assert.AreEqual(-1, select.SelectedIndex);

            select.Options[0].IsDefaultSelected = false;
            select.Options[0].IsDefaultSelected = true;
            Assert.AreEqual(0, select.SelectedIndex);
        }

        [TestCase("")]
        [TestCase("size=2")]
        public void FormResetUsesDisplaySizeAndSkipsDisabledFallbacks(String attributes)
        {
            var document = ("<form><select " + attributes + "><optgroup disabled><option>A</option></optgroup>" +
                "<option>B</option></select></form>").ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            select.Options[0].IsSelected = true;
            document.Forms[0].Reset();
            Assert.AreEqual(attributes.Length == 0 ? 1 : -1, select.SelectedIndex);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OptionClonePreservesCurrentSelectednessAndDirtiness(Boolean dirty)
        {
            var document = "<select size=2><option selected>A</option><option>B</option></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            var first = select.Options[0];

            if (dirty)
            {
                first.IsSelected = false;
            }
            else
            {
                select.Options[1].IsSelected = true;
            }

            var clone = (IHtmlOptionElement)first.Clone();
            Assert.IsFalse(clone.IsSelected);
            clone.IsDefaultSelected = false;
            clone.IsDefaultSelected = true;
            Assert.AreEqual(!dirty, clone.IsSelected);
            Assert.IsFalse(first.IsSelected);
        }

        [Test]
        public void SelectClonePreservesAnExplicitlyClearedSelection()
        {
            var document = "<select><option value=a>A</option><option value=b>B</option></select>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");
            select.Value = "missing";
            var clone = (IHtmlSelectElement)select.Clone(true);

            Assert.AreEqual(-1, clone.SelectedIndex);
            Assert.AreEqual(0, clone.SelectedOptions.Length);
            clone.Options[1].IsDefaultSelected = true;
            Assert.AreEqual("b", clone.Value);
            Assert.AreEqual(-1, select.SelectedIndex);
        }
    }
}
