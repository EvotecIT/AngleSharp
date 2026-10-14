namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using NUnit.Framework;
    using System;
    using System.Linq;

    [TestFixture]
    public class SelectSelectionTests
    {
        // https://html.spec.whatwg.org/multipage/form-elements.html#selectedness-setting-algorithm
        [TestCase("<option value='a'>A</option><option value='b'>B</option>", "", "a")]
        [TestCase("<option disabled value='a'>A</option><option value='b'>B</option>", "", "b")]
        [TestCase("<optgroup disabled><option value='a'>A</option></optgroup><option value='b'>B</option>", "", "b")]
        [TestCase("<option selected value='a'>A</option><option selected value='b'>B</option>", "", "b")]
        [TestCase("<option value='a'>A</option><option value='b'>B</option>", "size='2'", "")]
        [TestCase("<option value='a'>A</option><option value='b'>B</option>", "multiple", "")]
        [TestCase("<option selected value='a'>A</option><option selected value='b'>B</option>", "multiple", "a,b")]
        [TestCase("", "", "")]
        [TestCase("<option disabled value='a'>A</option>", "", "")]
        public void ParsedSelectInitializesSelectedness(String options, String attributes, String selectedValues)
        {
            var document = ("<select " + attributes + ">" + options + "</select>").ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");

            Assert.AreEqual(selectedValues, String.Join(",", select.SelectedOptions.Select(option => option.Value)));
            Assert.AreEqual(selectedValues.Split(',')[0], select.Value ?? String.Empty);
            Assert.AreEqual(selectedValues.Length == 0 ? -1 : select.Options.ToList().FindIndex(option => option.IsSelected), select.SelectedIndex);
        }

        [TestCase("innerHtml")]
        [TestCase("append")]
        [TestCase("add")]
        [TestCase("fragment")]
        public void PopulatingAnEmptySelectUpdatesSelectionAndSubmission(String route)
        {
            var document = "<form action='https://example.com/'><select name=choice></select></form>".ToHtmlDocument();
            var select = document.QuerySelector<IHtmlSelectElement>("select");

            if (route == "innerHtml")
            {
                select.InnerHtml = "<option value=a>A</option>";
            }
            else
            {
                var option = (IHtmlOptionElement)document.CreateElement("option");
                option.Value = "a";

                if (route == "add")
                {
                    select.AddOption(option);
                }
                else if (route == "fragment")
                {
                    var fragment = document.CreateDocumentFragment();
                    fragment.AppendChild(option);
                    select.AppendChild(fragment);
                }
                else
                {
                    select.AppendChild(option);
                }
            }

            Assert.AreEqual("a", select.Value);
            Assert.AreEqual(0, select.SelectedIndex);
            Assert.AreEqual(1, select.SelectedOptions.Length);
            Assert.AreEqual("choice=a", document.Forms[0].GetSubmission().Target.Query);
        }
    }
}
