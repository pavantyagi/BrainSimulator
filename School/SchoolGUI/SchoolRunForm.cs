﻿using GoodAI.BrainSimulator.Forms;
using GoodAI.Core.Execution;
using GoodAI.Core.Observers;
using GoodAI.Core.Utils;
using GoodAI.Modules.School.Common;
using GoodAI.Modules.School.Worlds;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace GoodAI.School.GUI
{
    public partial class SchoolRunForm : DockContent
    {
        public List<LearningTaskNode> Data;
        public List<LevelNode> Levels;
        public List<List<AttributeNode>> Attributes;
        public List<List<int>> AttributesChange;
        public PlanDesign Design;

        private List<DataGridView> LevelGrids;

        private readonly MainForm m_mainForm;
        private string m_runName;
        private SchoolWorld m_school;
        private ObserverForm m_observer;

        private int m_currentRow = -1;
        private int m_stepOffset = 0;
        private DateTime m_ltStart;

        private bool m_showObserver { get { return btnObserver.Checked; } }
        private bool m_emulateSuccess
        {
            set
            {
                if (m_school != null)
                    m_school.EmulatedUnitSuccessProbability = value ? 1f : 0f;
            }
        }

        public string RunName
        {
            get { return m_runName; }
            set
            {
                m_runName = value;

                Text = String.IsNullOrEmpty(m_runName) ? "School run" : "School run - " + m_runName;
            }
        }

        private LearningTaskNode CurrentTask
        {
            get
            {
                return Data.ElementAt(m_currentRow);
            }
        }


        public SchoolRunForm(MainForm mainForm)
        {
            m_mainForm = mainForm;
            InitializeComponent();

            btnObserver.Checked = Properties.School.Default.ShowVisual;

            // here so it does not interfere with designer generated code
            btnRun.Click += new System.EventHandler(m_mainForm.runToolButton_Click);
            btnStop.Click += new System.EventHandler(m_mainForm.stopToolButton_Click);
            btnPause.Click += new System.EventHandler(m_mainForm.pauseToolButton_Click);
            btnStepOver.Click += new System.EventHandler(m_mainForm.stepOverToolButton_Click);
            btnDebug.Click += new System.EventHandler(m_mainForm.debugToolButton_Click);

            m_mainForm.SimulationHandler.StateChanged += UpdateButtons;
            m_mainForm.SimulationHandler.ProgressChanged += SimulationHandler_ProgressChanged;
            UpdateButtons(null, null);
        }

        private void SimulationHandler_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (m_school == null)
                return;

            ILearningTask runningTask = m_school.CurrentLearningTask;
            if (runningTask == null)
                return;

            if (m_currentRow < 0 || runningTask.GetType() != Data.ElementAt(m_currentRow).TaskType) // next LT
                GoToNextTask();

            if (runningTask.GetType() != Data.ElementAt(m_currentRow).TaskType) //should not happen at all - just a safeguard
            {
                MyLog.ERROR.WriteLine("One of the Learning Tasks was skipped. Stopping simulation.");
                return;
            }

            UpdateTaskData(runningTask);
        }

        private void UpdateButtons(object sender, MySimulationHandler.StateEventArgs e)
        {
            btnRun.Enabled = m_mainForm.runToolButton.Enabled;
            btnPause.Enabled = m_mainForm.pauseToolButton.Enabled;
            btnStop.Enabled = m_mainForm.stopToolButton.Enabled;
        }

        public void Ready()
        {
            UpdateGridData();
            PrepareSimulation();
            SetObserver();
            if (Properties.School.Default.AutorunEnabled && Data != null)
                btnRun.PerformClick();
        }

        public void UpdateGridData()
        {
            dataGridView1.DataSource = Data;
            dataGridView1.Invalidate();
        }

        private void UpdateTaskData(ILearningTask runningTask)
        {
            CurrentTask.Steps = (int)m_mainForm.SimulationHandler.SimulationStep - m_stepOffset;
            CurrentTask.Progress = (int)runningTask.Progress;
            TimeSpan diff = DateTime.UtcNow - m_ltStart;
            CurrentTask.Time = (float)Math.Round(diff.TotalSeconds, 2);

            UpdateGridData();
        }

        private void GoToNextTask()
        {
            m_currentRow++;
            m_stepOffset = (int)m_mainForm.SimulationHandler.SimulationStep;
            m_ltStart = DateTime.UtcNow; ;

            HighlightCurrentTask();
        }

        private void SetObserver()
        {
            if (m_showObserver)
            {
                if (m_observer == null)
                {
                    try
                    {
                        MyMemoryBlockObserver observer = new MyMemoryBlockObserver();
                        observer.Target = m_school.Visual;

                        if (observer == null)
                            throw new InvalidOperationException("No observer was initialized");

                        m_observer = new ObserverForm(m_mainForm, observer, m_school);

                        m_observer.TopLevel = false;
                        observerDockPanel.Controls.Add(m_observer);

                        m_observer.CloseButtonVisible = false;
                        m_observer.MaximizeBox = false;
                        m_observer.Size = observerDockPanel.Size + new System.Drawing.Size(16, 38);
                        m_observer.Location = new System.Drawing.Point(-8, -30);

                        m_observer.Show();
                    }
                    catch (Exception e)
                    {
                        MyLog.ERROR.WriteLine("Error creating observer: " + e.Message);
                    }
                }
                else
                {
                    m_observer.Show();
                    observerDockPanel.Show();
                }
            }
            else
            {
                if (m_observer != null)
                {
                    observerDockPanel.Hide();
                }
            }
        }

        private void SelectSchoolWorld()
        {
            m_mainForm.SelectWorldInWorldList(typeof(SchoolWorld));
            m_school = (SchoolWorld)m_mainForm.Project.World;
            m_emulateSuccess = btnEmulateSuccess.Checked;   //set value AFTER the SchoolWorld creation
        }

        private void CreateCurriculum()
        {
            m_school.Curriculum = Design.AsSchoolCurriculum(m_school);
            // TODO: next two lines are probably not necessary
            foreach (ILearningTask task in m_school.Curriculum)
                task.SchoolWorld = m_school;
        }

        private void HighlightCurrentTask()
        {
            if (m_currentRow < 0)
                return;

            DataGridViewCellStyle defaultStyle = new DataGridViewCellStyle();
            DataGridViewCellStyle highlightStyle = new DataGridViewCellStyle();
            highlightStyle.BackColor = Color.PaleGreen;

            dataGridView1.Rows[m_currentRow].Selected = true;
            foreach (DataGridViewRow row in dataGridView1.Rows)
                foreach (DataGridViewCell cell in row.Cells)
                    if (row.Index == m_currentRow)
                        cell.Style = highlightStyle;
                    else
                        cell.Style = defaultStyle;
        }

        private void PrepareSimulation()
        {
            // data
            SelectSchoolWorld();
            CreateCurriculum();

            // gui
            m_stepOffset = 0;
            m_currentRow = -1;
            Data.ForEach(x => x.Steps = 0);
            Data.ForEach(x => x.Time = 0f);
            Data.ForEach(x => x.Progress = 0);
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            string columnName = dataGridView1.Columns[e.ColumnIndex].Name;
            if (columnName.Equals(TaskType.Name) || columnName.Equals(WorldType.Name))
            {
                // I am not sure about how bad this approach is, but it get things done
                if (e.Value != null)
                {
                    Type typeValue = e.Value as Type;

                    DisplayNameAttribute displayNameAtt = typeValue.GetCustomAttributes(typeof(DisplayNameAttribute), true).FirstOrDefault() as DisplayNameAttribute;
                    if (displayNameAtt != null)
                        e.Value = displayNameAtt.DisplayName;
                    else
                        e.Value = typeValue.Name;
                }
            }
        }

        private void SchoolRunForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                    {
                        btnRun.PerformClick();
                        break;
                    }
                case Keys.F7:
                    {
                        btnPause.PerformClick();
                        break;
                    }
                case Keys.F8:
                    {
                        btnStop.PerformClick();
                        break;
                    }
                case Keys.F10:
                    {
                        btnStepOver.PerformClick();
                        break;
                    }
            }
        }

        private void simulationStart(object sender, EventArgs e)
        {
            if (m_mainForm.SimulationHandler.State == MySimulationHandler.SimulationState.STOPPED)
                PrepareSimulation();
        }

        private LearningTaskNode SelectedLearningTask
        {
            get
            {
                int dataIndex;
                if (dataGridView1.SelectedRows != null && dataGridView1.SelectedRows.Count > 0)
                {
                    DataGridViewRow row = dataGridView1.SelectedRows[0];
                    dataIndex = row.Index;
                }
                else
                {
                    dataIndex = 0;
                }
                return Data[dataIndex];
            }
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            Invoke((MethodInvoker)(() =>
                {
                    LearningTaskNode ltNode = SelectedLearningTask;
                    Type ltType = ltNode.TaskType;
                    ILearningTask lt = LearningTaskFactory.CreateLearningTask(ltType);
                    TrainingSetHints hints = lt.TSProgression[0];

                    Levels = new List<LevelNode>();
                    LevelGrids = new List<DataGridView>();
                    Attributes = new List<List<AttributeNode>>();
                    AttributesChange = new List<List<int>>();
                    tabControl1.TabPages.Clear();

                    for (int i = 0; i < lt.TSProgression.Count; i++)
                    {
                        // create tab
                        LevelNode ln = new LevelNode(i + 1);
                        Levels.Add(ln);
                        TabPage tp = new TabPage(ln.Text);
                        tabControl1.TabPages.Add(tp);

                        // create grid
                        DataGridView dgv = new DataGridView();

                        dgv.Parent = tp;
                        dgv.Margin = new Padding(3);
                        dgv.Dock = DockStyle.Fill;
                        dgv.RowHeadersVisible = false;
                        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                        dgv.AllowUserToResizeRows = false;
                        // create attributes
                        Attributes.Add(new List<AttributeNode>());
                        if (i > 0)
                        {
                            hints.Set(lt.TSProgression[i]);
                        }
                        foreach (var attribute in hints)
                        {
                            AttributeNode an = new AttributeNode(
                                attribute.Key.Name,
                                attribute.Value,
                                attribute.Key.TypeOfValue);
                            Attributes[i].Add(an);
                        }

                        Attributes[i].Sort(Comparer<AttributeNode>.Create((x, y) => x.Name.CompareTo(y.Name)));
                        dgv.DataSource = Attributes[i];

                        dgv.Columns[0].Width = 249;
                        dgv.Columns[0].ReadOnly = true;
                        dgv.Columns[1].ReadOnly = true;

                        AttributesChange.Add(new List<int>());
                        if (i > 0)
                        {
                            foreach (var attribute in lt.TSProgression[i])
                            {
                                int attributeIdx = Attributes[i].IndexOf(new AttributeNode(attribute.Key.Name));
                                AttributesChange[i].Add(attributeIdx);
                            }
                        }

                        LevelGrids.Add(dgv);
                        dgv.ColumnWidthChanged += levelGridColumnSizeChanged;
                        dgv.CellFormatting += lGrid_CellFormatting;
                        dgv.SelectionChanged += levelGridSelectionChanged;

                        tabControl1.Update();
                    }
                }
            ));
        }

        private void levelGridSelectionChanged(object sender, EventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            dgv.ClearSelection();
        }

        private void lGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs args)
        {
            DataGridView dgv = sender as DataGridView;
            int i = LevelGrids.IndexOf(dgv);
            if (AttributesChange.Count == 0)
            {
                return;
            }
            if (AttributesChange[i].Contains(args.RowIndex))
            {
                args.CellStyle.BackColor = Color.LightGreen;
            }

            // unselect dgv
            dgv.ClearSelection();
        }

        private void levelGridColumnSizeChanged(object sender, DataGridViewColumnEventArgs e)
        {
            DataGridView dg = sender as DataGridView;
            for (int i = 0; i < dg.Columns.Count; i++)
            {
                int width = dg.Columns[i].Width;
                foreach (var levelGrid in LevelGrids)
                {
                    if (dg == levelGrid) continue;
                    levelGrid.Columns[i].Width = width;
                }
            }
        }

        private void btnObserver_Click(object sender, EventArgs e)
        {
            Properties.School.Default.ShowVisual = (sender as ToolStripButton).Checked = !(sender as ToolStripButton).Checked;
            Properties.School.Default.Save();
            SetObserver();
        }

        private void btnEmulateSuccess_Click(object sender, EventArgs e)
        {
            m_emulateSuccess = (sender as ToolStripButton).Checked = !(sender as ToolStripButton).Checked;
        }
    }
}
