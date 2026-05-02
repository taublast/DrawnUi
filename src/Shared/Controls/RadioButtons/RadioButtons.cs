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
            GroupsByName[groupName].Add(control);
            MakeAtLeastOneSelected(GroupsByName[groupName]);
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
            GroupsByParent[parent].Add(control);
            MakeAtLeastOneSelected(GroupsByParent[parent]);
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
                GroupsByName[groupName].Remove(control);
                MakeAtLeastOneSelected(GroupsByName[groupName]);
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
                GroupsByParent[parent].Remove(control);
                MakeAtLeastOneSelected(GroupsByParent[parent]);
            }
        }

        /// <summary>
        /// Removes a radio button control from all groups it belongs to. Ensures at least one button remains selected in affected groups.
        /// </summary>
        /// <param name="control">The radio button control to remove from all groups.</param>
        public void RemoveFromGroups(ISkiaRadioButton control)
        {
            bool removed = false;
            foreach (var groupName in GroupsByName.Keys.ToList())
            {
                var group = GroupsByName[groupName];
                if (group.Contains(control))
                {
                    group.Remove(control);
                    MakeAtLeastOneSelected(group);
                    removed = true;
                    break;
                }
            }

            if (!removed)
            {
                foreach (var parent in GroupsByParent.Keys.ToList())
                {
                    var group = GroupsByParent[parent];
                    if (group.Contains(control))
                    {
                        group.Remove(control);
                        MakeAtLeastOneSelected(group);
                        break;
                    }
                }
            }
        }

        private void MakeAtLeastOneSelected(List<ISkiaRadioButton> group)
        {
            if (!group.Any(c => c.GetValueInternal()))
            {
                var firstControl = group.FirstOrDefault();
                if (firstControl != null)
                {
                    firstControl.SetValueInternal(true);
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
                    SetGroupValuesExcept(groupName, control, newValue, false);
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
                    SetGroupValuesExcept(parent, control, newValue, true);
                    if (newValue)
                    {
                        Changed?.Invoke(control, EventArgs.Empty);
                    }
                    return;
                }
            }
        }

        private void SetGroupValuesExcept(string groupName, ISkiaRadioButton exceptControl, bool newValue, bool isGroupA)
        {
            var group = isGroupA ? GroupsByName[groupName] : null;

            if (newValue)
            {
                group?.ForEach(c => { if (c != exceptControl) c.SetValueInternal(false); });
            }
            else
            {
                EnsureOneTrueInGroup(group, exceptControl);
            }
        }

        private void SetGroupValuesExcept(SkiaControl parent, ISkiaRadioButton exceptControl, bool newValue, bool byParent)
        {
            var group = byParent ? GroupsByParent[parent] : null;

            if (newValue)
            {
                group?.ForEach(c => { if (c != exceptControl) c.SetValueInternal(false); });
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
