﻿/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for InsertTest
    /// </summary>
    [TestClass]
    public class InsertTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInsert()
        {
            TestFilesZip = @"TestFunctional\InsertTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Associate yeast background proteome
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunDlg<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg,
                buildBackgroundProteomeDlg =>
                {
                    buildBackgroundProteomeDlg.BackgroundProteomePath = TestFilesDir.GetTestPath(@"InsertTest\Yeast_MRMer.protdb");
                    buildBackgroundProteomeDlg.BackgroundProteomeName = "Yeast";
                    buildBackgroundProteomeDlg.OkDialog();
                });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForBackgroundProteomeLoaderCompleted(); // Allow protDB file to populate protein metadata first

            SetClipboardTextUI(PEPTIDES_CLIPBOARD_TEXT);

            using (new CheckDocumentState(6, 9, 9, 28))
            {
                // Keep all peptides.
                var insertPeptidesDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                PastePeptides(insertPeptidesDlg, BackgroundProteome.DuplicateProteinsFilter.AddToAll, true, true);
                Assert.AreEqual(10, insertPeptidesDlg.PeptideRowCount);
                Assert.IsTrue(insertPeptidesDlg.PeptideRowsContainProtein(string.IsNullOrEmpty));
                Assert.IsFalse(insertPeptidesDlg.PeptideRowsContainPeptide(string.IsNullOrEmpty));
                OkDialog(insertPeptidesDlg, insertPeptidesDlg.OkDialog);
            }

            {
                // Keep only first protein.
                var insertPeptidesDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                PastePeptides(insertPeptidesDlg, BackgroundProteome.DuplicateProteinsFilter.FirstOccurence, true, true);
                Assert.AreEqual(8, insertPeptidesDlg.PeptideRowCount);
                Assert.IsFalse(insertPeptidesDlg.PeptideRowsContainProtein(protein => Equals(protein, "YHR174W")));
                RunUI(insertPeptidesDlg.ClearRows);
                // Filter peptides with multiple matches.
                PastePeptides(insertPeptidesDlg, BackgroundProteome.DuplicateProteinsFilter.NoDuplicates, true, true);
                Assert.AreEqual(6, insertPeptidesDlg.PeptideRowCount);
                Assert.IsFalse(insertPeptidesDlg.PeptideRowsContainProtein(protein => Equals(protein, "YGR254W")));
                RunUI(insertPeptidesDlg.ClearRows);
                // Filter unmatched.
                PastePeptides(insertPeptidesDlg, BackgroundProteome.DuplicateProteinsFilter.AddToAll, false, true);
                Assert.IsFalse(insertPeptidesDlg.PeptideRowsContainProtein(string.IsNullOrEmpty));
                RunUI(insertPeptidesDlg.ClearRows);
                // Filter peptides not matching settings.
                PastePeptides(insertPeptidesDlg, BackgroundProteome.DuplicateProteinsFilter.AddToAll, true, false);
                Assert.AreEqual(9, insertPeptidesDlg.PeptideRowCount);
                Assert.IsFalse(insertPeptidesDlg.PeptideRowsContainPeptide(peptide =>
                    !SkylineWindow.Document.Settings.Accept(peptide)));
                RunUI(insertPeptidesDlg.ClearRows);
                // Pasting garbage should throw an error then disallow the paste.
                SetClipboardTextUI(PEPTIDES_CLIPBOARD_TEXT_GARBAGE);
                RunDlg<MessageDlg>(insertPeptidesDlg.PastePeptides, messageDlg => messageDlg.OkDialog());
                Assert.AreEqual(1, insertPeptidesDlg.PeptideRowCount);
                OkDialog(insertPeptidesDlg, insertPeptidesDlg.Close);
            }

            {
                var pasteText = TransitionsClipboardText;
                var transitionDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => transitionDlg.TransitionListText = pasteText);
                WaitForConditionUI(() => columnSelectDlg.WindowShown); // Avoids possible race condition in code coverage tests
                var associateProteinsDlg = ShowDialog<FilterMatchedPeptidesDlg>(columnSelectDlg.OkDialog); // Some peptides aren't in background proteome
                var errDlg = ShowDialog<ImportTransitionListErrorDlg>(associateProteinsDlg.OkDialog);
                RunUI(() =>
                {
                    var allErrorText = TextUtil.LineSeparate(errDlg.ErrorList.Select(e => e.ErrorMessage));
                    Assert.AreEqual(12, errDlg.ErrorList.Count, string.Format("Expecting 12 errors\r\ngot \"{0}\"", allErrorText));
                    const double expectedPrecursor = 506.7821;
                    string expectedPrecursorText = expectedPrecursor.ToString(LocalizationHelper.CurrentCulture);
                    Assert.IsTrue(allErrorText.Contains(expectedPrecursorText),
                        string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                            expectedPrecursorText, allErrorText));
                });
                OkDialog(errDlg, errDlg.CancelDialog);
                OkDialog(columnSelectDlg, columnSelectDlg.CancelDialog);
            }

            {
                // Test modification matching
                var heavyMod = new[]
                {
                    new TypedModifications(IsotopeLabelType.heavy, new List<StaticMod>
                    {
                        new StaticMod("Label:13C(6)15N(4)", "R", null, null, LabelAtoms.C13 | LabelAtoms.N15, null, null)
                    })
                };
                SrmDocument document = SkylineWindow.Document;
                RunUI(() =>
                {
                    SkylineWindow.ModifyDocument("Add modification", doc =>
                        doc.ChangeSettings(doc.Settings.ChangePeptideModifications(mods => new PeptideModifications(mods.StaticModifications, heavyMod))));
                    SkylineWindow.Document.Settings.UpdateDefaultModifications(true);
                });
                WaitForDocumentChange(document);

                var pasteText = "LGPGRPLPTFPTSEC[+57]TS[+80]DVEPDTR[+10]\t907.081803\t1387.566968\tDDX54_CL02".Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                var transitionDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => transitionDlg.TransitionListText = pasteText);
                WaitForConditionUI(() => columnSelectDlg.WindowShown); // Avoids possible race condition in code coverage tests
                var associateProteinsDlg = ShowDialog<FilterMatchedPeptidesDlg>(() => columnSelectDlg.CheckForErrors()); // Some peptides aren't in background proteome
                var noErrDlg = ShowDialog<MessageDlg>(associateProteinsDlg.OkDialog);
                Assert.AreEqual(Skyline.Properties.Resources.PasteDlg_ShowNoErrors_No_errors, noErrDlg.Message);
                OkDialog(noErrDlg, noErrDlg.OkDialog);
                OkDialog(columnSelectDlg, columnSelectDlg.CancelDialog);
            }
        }

        private static void PastePeptides(PasteDlg pasteDlg, BackgroundProteome.DuplicateProteinsFilter duplicateProteinsFilter, 
            bool addUnmatched, bool addFiltered)
        {
            RunDlg<FilterMatchedPeptidesDlg>(pasteDlg.PastePeptides, filterMatchedPeptidesDlg =>
            {
                filterMatchedPeptidesDlg.DuplicateProteinsFilter = duplicateProteinsFilter;
                filterMatchedPeptidesDlg.AddUnmatched = addUnmatched;
                filterMatchedPeptidesDlg.AddFiltered = addFiltered;
                filterMatchedPeptidesDlg.OkDialog();
            });
        }
        
        private string TransitionsClipboardText
        {
            get
            {
                return TRANSITIONS_CLIPBOARD_TEXT.Replace(".",
                    LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            }
        }

        private const string PEPTIDES_CLIPBOARD_TEXT =
            "SIVPSGASTGVHEALEMR\t\r\nSGETEDTFIADLVVGLR\t\r\nTANDVLTIR\tPROTEIN\t\r\nVQSAVLGFPR"
            + "\t\r\n\t\r\nVVVFEDAPAGIAAGK\t\r\nYHIEEEGSR\t\r\nLERLTSLNVVAGSDLR";

        private const string PEPTIDES_CLIPBOARD_TEXT_GARBAGE =
            "SIVPSGASTGVHEALEMR\t\r\nSGETEDTFIADLVVGLR\t\r\nTANDVLTIR\tPROTEIN\t\r\nVQSAVLGFPR"
            + "\t\r\n;;\t\r\nVVVFEDAPAGIAAGK\t\r\nYHIEEEGSR\t\r\nLERLTSLNVVAGSDLR";

        private const string TRANSITIONS_CLIPBOARD_TEXT =
            @"TANDVLTIR	501.778	830.474
TANDVLTIR	506.7821345	840.482269
TANDVLTIR	501.778	601.4072
TANDVLTIR	506.7821345	611.415469
TANDVLTIR	501.778	389.2511
TANDVLTIR	506.7821345	399.259369
VQSAVLGFPR	537.308	846.4799
VQSAVLGFPR	542.3121345	856.488169
VQSAVLGFPR	537.308	589.3481
VQSAVLGFPR	542.3121345	599.356369
VQSAVLGFPR	537.308	688.4166
VQSAVLGFPR	542.3121345	698.424869
YHIEEEGSR	560.2523	819.3851
YHIEEEGSR	565.2564345	829.393369
YHIEEEGSR	560.2523	706.3009
YHIEEEGSR	565.2564345	716.309169
VVVFEDAPAGIAAGK	722.3993	684.404
VVVFEDAPAGIAAGK	726.4063995	692.418199
VVVFEDAPAGIAAGK	722.3993	1146.5833
VVVFEDAPAGIAAGK	726.4063995	1154.597499
VVVFEDAPAGIAAGK	722.3993	1245.6534
VVVFEDAPAGIAAGK	726.4063995	1253.667599
VVVFEDAPAGIAAGK	722.3993	870.4706
VVVFEDAPAGIAAGK	726.4063995	878.484799";

        private const int NUM_UNMATCHED_EXPECTED = 2;

    }
}
