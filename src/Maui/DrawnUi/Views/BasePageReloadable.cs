namespace DrawnUi.Views;

/// <summary>
/// Base class for a page with canvas, supports C# HotReload for building UI with code (not XAML).
/// Override `Build()`, see examples.
/// 
/// </summary>
public class BasePageReloadable : DrawnUiBasePage, IDisposable
{

    public BasePageReloadable()
    {
        if (Debugger.IsAttached)
        {
            Super.HotReload += ReloadUi;
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler != null)
        {
            if (!wasBuilt)
            {
                Build();
            }
            if (Debugger.IsAttached)
            {
                Super.HotReload += ReloadUi;
            }
        }
        else
        {
            Super.HotReload -= ReloadUi;
        }
    }

    private bool wasBuilt;

    public int CountReloads { get; protected set; }

    /// <summary>
    /// Reload code-behind constructed page
    /// </summary>
    /// <param name="obj"></param>
    protected virtual void ReloadUi(Type[] obj)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CountReloads++;
            Build();
        });
    }

    /// <summary>
    /// Build code-behind constructed page
    /// </summary>
    public virtual void Build()
    {
        wasBuilt = true;
    }

    public void Dispose()
    {
	    Dispose(true);
	    GC.SuppressFinalize(this);
	}

    public bool IsDisposed { get; protected  set; }

    protected virtual void Dispose(bool isDisposing)
    {
	    if (!IsDisposed && isDisposing)
	    {
		    IsDisposed = true;

            Super.HotReload -= ReloadUi;

            foreach (var child in InternalChildren)
		    {
			    if (child is IDisposable disposable)
			    {
				    disposable?.Dispose();
			    }
			    else
			    if (child is Grid grid)
			    {
				    foreach (var gridChild in grid.Children)
				    {
					    if (gridChild is IDisposable disposableChild)
					    {
						    disposableChild?.Dispose();
					    }
				    }
			    }
			    else
			    if (child is StackLayout stack)
			    {
				    foreach (var stackChild in stack.Children)
				    {
					    if (stackChild is IDisposable disposableChild)
					    {
						    disposableChild?.Dispose();
					    }
				    }
			    }
		    }
		}

    }
}
