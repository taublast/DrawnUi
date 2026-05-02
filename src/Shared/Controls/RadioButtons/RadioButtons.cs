namespace DrawnUi.Controls
{
    /// <summary>
    /// Manages radio button groups, ensuring only one button is selected per group.
    /// Supports grouping by parent control or by string name.
    /// </summary>
    public class RadioButtons
    {
        static RadioButtons _instance;

        /// <summary>
        /// Gets the singleton instance of the RadioButtons manager.
        /// </summary>
        public static RadioButtons All
        {
            get
            {
                if (_instance == null)
                    _instance = new RadioButtons();

                return _instance;
            }
        }

        /// <summary>
        /// Occurs when a radio button selection changes in any group.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>
        /// Gets the currently selected radio button in the group associated with the specified parent control.
        /// </summary>
        /// <param name="parent">The parent control that defines the radio button group.</param>
        /// <returns>The selected SkiaControl, or null if no button is selected or group doesn't exist.</returns>
        public SkiaControl GetSelected(SkiaControl parent)
        {
            var group = GroupsByParent[parent];

            if (group == null)
            {
                return null;
            }
            return group.FirstOrDefault(c => c.GetValueInternal()) as SkiaControl;
        }

        /// <summary>
        /// Gets the currently selected radio button in the group with the specified name.
        /// </summary>
        /// <param name="groupName">The name of the radio button group.</param>
        /// <returns>The selected SkiaControl, or null if no button is selected or group doesn't exist.</returns>
        public SkiaControl GetSelected(string groupName)
        {
            var group = GroupsByName[groupName];

            if (group == null)
            {
                return null;
            }
            return group.FirstOrDefault(c => c.GetValueInternal()) as SkiaControl;
        }

        /// <summary>
        /// Gets the index of the currently selected radio button in the group associated with the specified parent control.
        /// </summary>
        /// <param name="parent">The parent control that defines the radio button group.</param>
        /// <returns>The zero-based index of the selected button, or -1 if no button is selected or group doesn't exist.</returns>
        public int GetSelectedIndex(SkiaControl parent)
        {
            var group = GroupsByParent[parent];
            if (group == null)
            {
                return -1;
            }
            return GetSelectedIndexInternal(group);
        }

        /// <summary>
        /// Gets the index of the currently selected radio button in the group with the specified name.
        /// </summary>
        /// <param name="groupName">The name of the radio button group.</param>
        /// <returns>The zero-based index of the selected button, or -1 if no button is selected or group doesn't exist.</returns>
        public int GetSelectedIndex(string groupName)
        {
            var group = GroupsByName[groupName];
            if (group == null)
            {
                return -1;
            }
            return GetSelectedIndexInternal(group);
        }

        int GetSelectedIndexInternal(List<ISkiaRadioButton> group)
        {
            var index = -1;
            foreach (ISkiaRadioButton radio in group)
            {
                index++;
                if (radio.GetValueInternal())
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// Selects the radio button at the specified index in the group associated with the container control.
        /// </summary>
        /// <param name="container">The parent control that defines the radio button group.</param>
        /// <param name="index">The zero-based index of the button to select.</param>
        public void Select(SkiaControl container, int index)
        {
            var group = GroupsByParent[container];
            if (group != null)
            {
                var i = -1;
                foreach (ISkiaRadioButton radio in group)
                {
                    i++;
                    radio.SetValueInternal(index == i);
                }
            }
        }

        protected Dictionary<string, List<ISkiaRadioButton>> GroupsByName { get; private set; }
        protected Dictionary<SkiaControl, List<ISkiaRadioButton>> GroupsByParent { get; private set; }

        /// <summary>
        /// Initializes a new instance of the RadioButtons class.
        /// </summary>
        public RadioButtons()
        {
            GroupsByName = new Dictionary<string, List<ISkiaRadioButton>>();
            GroupsByParent = new Dictionary<SkiaControl, List<ISkiaRadioButton>>();
        }

        /// <summary>
        /// Adds a radio button control to a named group. Ensures at least one button in the group is selected.
        /// </summary>
        /// <param name="control">The radio button control to add to the group.</param>
        /// <param name="groupName">The name of the group to add the control to.</param>
        public void AddToGroup(ISkiaRadioButton control, string groupName)
        {
            if (!GroupsByName.ContainsKey(groupName))
            {
                GroupsByName[groupName] = new();
            }

            var group = GroupsByName[groupName];
            if (!group.Contains(control))
            {
                group.Add(control);
            }

            NormalizeGroupSelection(group);
            SyncGroupVisuals(group);
        }

        /// <summary>
        /// Adds a radio button control to a parent-based group. Ensures at least one button in the group is selected.
        /// </summary>
        /// <param name="control">The radio button control to add to the group.</param>
        /// <param name="parent">The parent control that defines the group.</param>
        public void AddToGroup(ISkiaRadioButton control, SkiaControl parent)
        {
            if (!GroupsByParent.ContainsKey(parent))
            {
                GroupsByParent[parent] = new();
            }

            var group = GroupsByParent[parent];
            if (!group.Contains(control))
            {
                group.Add(control);
            }

            NormalizeGroupSelection(group);
            SyncGroupVisuals(group);
        }

        /// <summary>
        /// Removes a radio button control from a named group. Ensures at least one button remains selected in the group.
        /// </summary>
        /// <param name="groupName">The name of the group to remove the control from.</param>
        /// <param name="control">The radio button control to remove.</param>
        public void RemoveFromGroup(string groupName, ISkiaRadioButton control)
        {
            if (GroupsByName.ContainsKey(groupName) && GroupsByName[groupName].Contains(control))
            {
                var group = GroupsByName[groupName];
                group.RemoveAll(radio => radio == control);
                NormalizeGroupSelection(group);
                SyncGroupVisuals(group);
            }
        }

        /// <summary>
        /// Removes a radio button control from a parent-based group. Ensures at least one button remains selected in the group.
        /// </summary>
        /// <param name="parent">The parent control that defines the group to remove the control from.</param>
        /// <param name="control">The radio button control to remove.</param>
        public void RemoveFromGroup(SkiaControl parent, ISkiaRadioButton control)
        {
            if (GroupsByParent.ContainsKey(parent) && GroupsByParent[parent].Contains(control))
            {
                var group = GroupsByParent[parent];
                group.RemoveAll(radio => radio == control);
                NormalizeGroupSelection(group);
                SyncGroupVisuals(group);
            }
        }

        /// <summary>
        /// Removes a radio button control from all groups it belongs to. Ensures at least one button remains selected in affected groups.
        /// </summary>
        /// <param name="control">The radio button control to remove from all groups.</param>
        public void RemoveFromGroups(ISkiaRadioButton control)
        {
            foreach (var groupName in GroupsByName.Keys.ToList())
            {
                var group = GroupsByName[groupName];
                if (group.RemoveAll(radio => radio == control) > 0)
                {
                    NormalizeGroupSelection(group);
                    SyncGroupVisuals(group);
                }
            }

            foreach (var parent in GroupsByParent.Keys.ToList())
            {
                var group = GroupsByParent[parent];
                if (group.RemoveAll(radio => radio == control) > 0)
                {
                    NormalizeGroupSelection(group);
                    SyncGroupVisuals(group);
                }
            }
        }

        private void NormalizeGroupSelection(List<ISkiaRadioButton> group)
        {
            if (group == null || group.Count == 0)
            {
                return;
            }

            var selected = group.Where(c => c.GetValueInternal()).ToList();
            if (selected.Count == 0)
            {
                group[0].SetValueInternal(true);
                return;
            }

            var keeper = selected[0];
            foreach (var radio in selected)
            {
                if (!ReferenceEquals(radio, keeper))
                {
                    radio.SetValueInternal(false);
                }
            }
        }

        private static void SyncGroupVisuals(List<ISkiaRadioButton> group)
        {
            if (group == null)
            {
                return;
            }

            foreach (var radio in group)
            {
                if (radio is SkiaToggle toggle)
                {
                    toggle.ApplyProperties();
                }
            }
        }

        /// <summary>
        /// Called by radio button controls to report value changes. Manages mutual exclusion within groups and fires the Changed event.
        /// </summary>
        /// <param name="control">The radio button control reporting the change.</param>
        /// <param name="newValue">The new value of the control (true for selected, false for unselected).</param>
        public void ReportValueChange(ISkiaRadioButton control, bool newValue)
        {
            foreach (var groupName in GroupsByName.Keys)
            {
                if (GroupsByName[groupName].Contains(control))
                {
                    SetGroupValuesExcept(groupName, control, newValue);
                    if (newValue)
                    {
                        Changed?.Invoke(control, EventArgs.Empty);
                    }
                    return;
                }
            }

            foreach (var parent in GroupsByParent.Keys)
            {
                if (GroupsByParent[parent].Contains(control))
                {
                    SetGroupValuesExcept(parent, control, newValue);
                    if (newValue)
                    {
                        Changed?.Invoke(control, EventArgs.Empty);
                    }
                    return;
                }
            }
        }

        private void SetGroupValuesExcept(string groupName, ISkiaRadioButton exceptControl, bool newValue)
        {
            var group = GroupsByName[groupName];

            if (newValue)
            {
                group.ForEach(c => { if (c != exceptControl) c.SetValueInternal(false); });
            }
            else
            {
                EnsureOneTrueInGroup(group, exceptControl);
            }
        }

        private void SetGroupValuesExcept(SkiaControl parent, ISkiaRadioButton exceptControl, bool newValue)
        {
            var group = GroupsByParent[parent];

            if (newValue)
            {
                group.ForEach(c => { if (c != exceptControl) c.SetValueInternal(false); });
            }
            else
            {
                EnsureOneTrueInGroup(group, exceptControl);
            }
        }

        private void EnsureOneTrueInGroup(List<ISkiaRadioButton> group, ISkiaRadioButton exceptControl)
        {
            var trueControls = group?.Where(c => c.GetValueInternal()).ToList();

            if (trueControls == null || trueControls.Count == 0 || (trueControls.Count == 1 && trueControls.Contains(exceptControl)))
            {
                var controlToSetTrue = group?.FirstOrDefault(c => c != exceptControl);
                controlToSetTrue?.SetValueInternal(true);
            }
        }
    }
}
